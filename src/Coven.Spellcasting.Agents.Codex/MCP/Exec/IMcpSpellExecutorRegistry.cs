using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.MCP.Exec;

public interface IMcpSpellExecutorRegistry
{
    IReadOnlyList<McpTool> Tools { get; }

    bool TryInvoke(
        string name,
        JsonElement? args,
        CancellationToken ct,
        out Task<object?> resultTask,
        out bool returnsJson);
}
