// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.MCP.Exec;

/// <summary>
/// Minimal executor that maps spell names to ISpellContract instances and invokes them.
/// Uses straightforward reflection per call to deserialize args and dispatch to the right CastSpell overload.
/// Prioritizes simplicity over precompiled delegates.
/// </summary>
internal sealed class SimpleMcpSpellExecutorRegistry : IMcpSpellExecutorRegistry
{
    private readonly Dictionary<string, ISpellContract> _byName = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<McpTool> Tools { get; }

    public SimpleMcpSpellExecutorRegistry(IEnumerable<ISpellContract> spells)
    {
        var tools = new List<McpTool>();
        foreach (var s in spells)
        {
            _byName[s.Definition.Name] = s;
            tools.Add(new McpTool(s.Definition.Name, s.Definition.InputSchema, s.Definition.OutputSchema));
        }
        Tools = tools;
    }

    public bool TryInvoke(string name, JsonElement? args, CancellationToken ct, out Task<object?> resultTask, out bool returnsJson)
    {
        if (!_byName.TryGetValue(name, out var spell))
        {
            resultTask = Task.FromResult<object?>(null);
            returnsJson = false;
            return false;
        }

        // Zero-arg spell
        if (spell is ISpell zero)
        {
            resultTask = InvokeZeroAsync(zero);
            returnsJson = false;
            return true;
        }

        // Prefer binary generic spell if present; otherwise unary
        var interfaces = spell.GetType().GetInterfaces();

        var bin = interfaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISpell<,>));
        if (bin is not null)
        {
            var argsTypes = bin.GetGenericArguments();
            var tIn = argsTypes[0];
            var tOut = argsTypes[1];
            resultTask = InvokeBinaryAsync(spell, bin, tIn, tOut, args);
            returnsJson = tOut != typeof(string);
            return true;
        }

        var uni = interfaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISpell<>));
        if (uni is not null)
        {
            var tIn = uni.GetGenericArguments()[0];
            resultTask = InvokeUnaryAsync(spell, uni, tIn, args);
            returnsJson = false;
            return true;
        }

        resultTask = Task.FromResult<object?>(null);
        returnsJson = false;
        return false;
    }

    private static async Task<object?> InvokeZeroAsync(ISpell spell)
    {
        await spell.CastSpell().ConfigureAwait(false);
        return null;
    }

    private static async Task<object?> InvokeUnaryAsync(object spell, Type iface, Type tIn, JsonElement? args)
    {
        var input = Deserialize(args, tIn);
        var method = iface.GetMethod("CastSpell")!; // Task
        var task = (Task)method.Invoke(spell, new[] { input })!;
        await task.ConfigureAwait(false);
        return null;
    }

    private static async Task<object?> InvokeBinaryAsync(object spell, Type iface, Type tIn, Type tOut, JsonElement? args)
    {
        var input = Deserialize(args, tIn);
        var method = iface.GetMethod("CastSpell")!; // Task<TOut>
        var taskObj = method.Invoke(spell, new[] { input })!;
        var task = (Task)taskObj;
        await task.ConfigureAwait(false);
        // extract Task<T>.Result via reflection
        var resultProp = taskObj.GetType().GetProperty("Result")!;
        return resultProp.GetValue(taskObj);
    }

    private static object? Deserialize(JsonElement? args, Type tIn)
    {
        if (args is JsonElement el)
        {
            return JsonSerializer.Deserialize(el, tIn, options: null);
        }
        return tIn.IsValueType ? Activator.CreateInstance(tIn) : null;
    }
}

