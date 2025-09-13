// SPDX-License-Identifier: BUSL-1.1

using System.Linq.Expressions;
using System.Text.Json;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.MCP.Exec;

internal sealed class ReflectionMcpSpellExecutorRegistry : IMcpSpellExecutorRegistry
{
    private readonly Dictionary<string, (Func<JsonElement?, CancellationToken, Task<object?>> Exec, bool ReturnsJson)> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<McpTool> _tools = new();
    public IReadOnlyList<McpTool> Tools => _tools;

    public ReflectionMcpSpellExecutorRegistry(IEnumerable<object> spells)
    {
        foreach (var s in spells)
        {
            TryRegisterSpell(s);
        }
    }

    public bool TryInvoke(string name, JsonElement? args, CancellationToken ct, out Task<object?> resultTask, out bool returnsJson)
    {
        if (_map.TryGetValue(name, out var entry))
        {
            returnsJson = entry.ReturnsJson;
            resultTask = entry.Exec(args, ct);
            return true;
        }
        resultTask = Task.FromResult<object?>(null);
        returnsJson = false;
        return false;
    }

    private void TryRegisterSpell(object spell)
    {
        var t = spell.GetType();
        foreach (var iface in t.GetInterfaces())
        {
            if (!iface.IsGenericType) continue;
            var gd = iface.GetGenericTypeDefinition();

            if (gd == typeof(ISpell<,>))
            {
                var args = iface.GetGenericArguments();
                RegisterBinary(spell, iface, args[0], args[1]);
            }
            else if (gd == typeof(ISpell<>))
            {
                var args = iface.GetGenericArguments();
                RegisterUnary(spell, iface, args[0]);
            }
        }

        // Zero-arg spell
        if (spell is ISpell zero)
        {
            RegisterZero(spell);
        }
    }

    private void RegisterBinary(object spell, Type iface, Type tIn, Type tOut)
    {
        var name = (spell as ISpellContract)?.GetDefinition().Name ?? SchemaGen.GetFriendlyName(tIn);

        var exec = BuildBinaryExecutor(spell, iface, tIn, tOut);

        bool returnsJson = tOut != typeof(string);
        _map[name] = (exec, returnsJson);
    }

    private Func<JsonElement?, CancellationToken, Task<object?>> BuildBinaryExecutor(object spell, Type iface, Type tIn, Type tOut)
    {
        var spellParam = Expression.Constant(spell);
        var jeParam = Expression.Parameter(typeof(JsonElement?), "args");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        // Convert args -> TIn via JsonSerializer.Deserialize<TIn>(JsonElement)
        var jeVar = Expression.Parameter(typeof(JsonElement), "je");
        var getValueOrDefault = Expression.Condition(
            Expression.Property(jeParam, "HasValue"),
            Expression.Property(jeParam, "Value"),
            Expression.Default(typeof(JsonElement))
        );
        var assignJe = Expression.Assign(jeVar, getValueOrDefault);

        var deserializeGeneric = typeof(JsonSerializer).GetMethods()
            .First(m => m.Name == nameof(JsonSerializer.Deserialize) && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(JsonElement));
        var deserialize = deserializeGeneric.MakeGenericMethod(tIn);
        var inputVar = Expression.Parameter(tIn, "input");
        var callDeserialize = Expression.Assign(inputVar, Expression.Call(deserialize, jeVar, Expression.Constant(null, typeof(JsonSerializerOptions))));

        // Call iface.CastSpell(TIn)
        var method = iface.GetMethod("CastSpell")!; // Task<TOut>
        var call = Expression.Call(Expression.Convert(spellParam, iface), method, inputVar);

        // Wrap to Task<object?> via helper
        var wrap = typeof(ReflectionMcpSpellExecutorRegistry).GetMethod(nameof(WrapTask), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.MakeGenericMethod(tOut);
        var callWrap = Expression.Call(wrap, call);

        var body = Expression.Block(new[] { jeVar, inputVar }, assignJe, callDeserialize, callWrap);
        var del = Expression.Lambda<Func<JsonElement?, CancellationToken, Task<object?>>>(body, jeParam, ctParam).Compile();
        return del;
    }

    private void RegisterUnary(object spell, Type iface, Type tIn)
    {
        var name = (spell as ISpellContract)?.GetDefinition().Name ?? SchemaGen.GetFriendlyName(tIn);

        var exec = BuildUnaryExecutor(spell, iface, tIn);
        _map[name] = (exec, false);
    }

    private Func<JsonElement?, CancellationToken, Task<object?>> BuildUnaryExecutor(object spell, Type iface, Type tIn)
    {
        var spellParam = Expression.Constant(spell);
        var jeParam = Expression.Parameter(typeof(JsonElement?), "args");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var getJeOrDefault = Expression.Condition(
            Expression.Property(jeParam, "HasValue"),
            Expression.Property(jeParam, "Value"),
            Expression.Default(typeof(JsonElement))
        );

        var deserializeGeneric = typeof(JsonSerializer).GetMethods()
            .First(m => m.Name == nameof(JsonSerializer.Deserialize) && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(JsonElement));
        var deserialize = deserializeGeneric.MakeGenericMethod(tIn);
        var inputVar = Expression.Variable(tIn, "input");
        var assignInput = Expression.Assign(inputVar, Expression.Call(deserialize, getJeOrDefault, Expression.Constant(null, typeof(JsonSerializerOptions))));

        var method = iface.GetMethod("CastSpell")!; // Task
        var call = Expression.Call(Expression.Convert(spellParam, iface), method, inputVar);
        var wrap = typeof(ReflectionMcpSpellExecutorRegistry).GetMethod(nameof(WrapTaskVoid), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var callWrap = Expression.Call(wrap, call);

        var body = Expression.Block(new[] { inputVar }, assignInput, callWrap);
        return Expression.Lambda<Func<JsonElement?, CancellationToken, Task<object?>>>(body, jeParam, ctParam).Compile();
    }

    private void RegisterZero(object spell)
    {
        var name = (spell as ISpellContract)?.GetDefinition().Name ?? SchemaGen.GetFriendlyName(spell.GetType());
        var exec = BuildZeroExecutor(spell);
        _map[name] = (exec, false);
    }

    private Func<JsonElement?, CancellationToken, Task<object?>> BuildZeroExecutor(object spell)
    {
        var spellParam = Expression.Constant(spell);
        var jeParam = Expression.Parameter(typeof(JsonElement?), "args");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");
        var method = typeof(ISpell).GetMethod("CastSpell")!;
        var call = Expression.Call(Expression.Convert(spellParam, typeof(ISpell)), method);
        var wrap = typeof(ReflectionMcpSpellExecutorRegistry).GetMethod(nameof(WrapTaskVoid), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var callWrap = Expression.Call(wrap, call);
        return Expression.Lambda<Func<JsonElement?, CancellationToken, Task<object?>>>(callWrap, jeParam, ctParam).Compile();
    }

    private static async Task<object?> WrapTask<T>(Task<T> task) => await task.ConfigureAwait(false);
    private static async Task<object?> WrapTaskVoid(Task task) { await task.ConfigureAwait(false); return null; }
}
