// SPDX-License-Identifier: BUSL-1.1

using System.Reflection;
using System.Text.Json;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.MCP.Tools;

/// <summary>
/// Executor that maps spell names to pre-bound delegates.
/// Reflection is used once at registration to bind delegates; invocations are reflection-free.
/// </summary>
internal sealed class SimpleMcpSpellExecutorRegistry : IMcpSpellExecutorRegistry
{
    private sealed record Entry(
        Func<JsonElement?, CancellationToken, Task<object?>> Invoker,
        bool ReturnsJson);

    private readonly Dictionary<string, Entry> _exec = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<McpTool> Tools { get; }

    public SimpleMcpSpellExecutorRegistry(IEnumerable<ISpellContract> spells)
    {
        var tools = new List<McpTool>();

        foreach (var s in spells)
        {
            var def = s.Definition;
            tools.Add(new McpTool(def.Name, def.InputSchema, def.OutputSchema));

            if (s is ISpell zero)
            {
                _exec[def.Name] = new Entry(
                    async (args, ct) => { await zero.CastSpell().ConfigureAwait(false); return null; },
                    ReturnsJson: false);
                continue;
            }

            var t = s.GetType();
            var ifaces = t.GetInterfaces();

            var bin = ifaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISpell<,>));
            if (bin is not null)
            {
                var ga = bin.GetGenericArguments();
                var tIn = ga[0];
                var tOut = ga[1];
                var binder = typeof(SimpleMcpSpellExecutorRegistry)
                    .GetMethod(nameof(BindBinary), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(tIn, tOut);
                var invoker = (Func<JsonElement?, CancellationToken, Task<object?>>)binder.Invoke(null, new object[] { s })!;
                var returnsJson = tOut != typeof(string);
                _exec[def.Name] = new Entry(invoker, returnsJson);
                continue;
            }

            var uni = ifaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISpell<>));
            if (uni is not null)
            {
                var tIn = uni.GetGenericArguments()[0];
                var binder = typeof(SimpleMcpSpellExecutorRegistry)
                    .GetMethod(nameof(BindUnary), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(tIn);
                var invoker = (Func<JsonElement?, CancellationToken, Task<object?>>)binder.Invoke(null, new object[] { s })!;
                _exec[def.Name] = new Entry(invoker, ReturnsJson: false);
            }
        }

        Tools = tools;
    }

    public bool TryInvoke(string name, JsonElement? args, CancellationToken ct, out Task<object?> resultTask, out bool returnsJson)
    {
        if (_exec.TryGetValue(name, out var entry))
        {
            returnsJson = entry.ReturnsJson;
            resultTask = entry.Invoker(args, ct);
            return true;
        }

        resultTask = Task.FromResult<object?>(null);
        returnsJson = false;
        return false;
    }

    // ---- binding helpers (generic; used once per spell at registration) ----
    private static Func<JsonElement?, CancellationToken, Task<object?>> BindUnary<TIn>(ISpell<TIn> spell)
    {
        return async (args, ct) =>
        {
            var input = args is JsonElement el ? JsonSerializer.Deserialize<TIn>(el, options: null)! : default!;
            await spell.CastSpell(input).ConfigureAwait(false);
            return null;
        };
    }

    private static Func<JsonElement?, CancellationToken, Task<object?>> BindBinary<TIn, TOut>(ISpell<TIn, TOut> spell)
    {
        return async (args, ct) =>
        {
            var input = args is JsonElement el ? JsonSerializer.Deserialize<TIn>(el, options: null)! : default!;
            var result = await spell.CastSpell(input).ConfigureAwait(false);
            return (object?)result;
        };
    }
}
