// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using Coven.FileSystem;
using Coven.Transmutation;

namespace Coven.Agents.FileSystem;

/// <summary>
/// Forward transmuter: converts AgentToolCall → FileRead.
/// Returns null (via exception filtering at the covenant level) when the tool name doesn't match.
/// </summary>
internal sealed class AgentToolCallToFileRead : ITransmuter<AgentToolCall, FileRead>
{
    /// <inheritdoc />
    public Task<FileRead> Transmute(AgentToolCall Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return !string.Equals(Input.ToolName, "read_file", StringComparison.Ordinal)
            ? throw new InvalidOperationException(
                $"AgentToolCallToFileRead cannot handle tool '{Input.ToolName}'.")
            : Task.FromResult(new FileRead(Input.CorrelationId, ExtractPath(Input.ArgumentsJson)));
    }

    private static string ExtractPath(string argumentsJson)
    {
        using JsonDocument doc = JsonDocument.Parse(argumentsJson);
        return doc.RootElement.TryGetProperty("path", out JsonElement pathElement)
            ? pathElement.GetString() ?? throw new ArgumentException("Tool argument 'path' is null.")
            : throw new ArgumentException("Tool argument 'path' is required.");
    }
}
