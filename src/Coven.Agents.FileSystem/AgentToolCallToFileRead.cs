// SPDX-License-Identifier: BUSL-1.1

using Coven.FileSystem;
using Coven.Transmutation;

namespace Coven.Agents.FileSystem;

/// <summary>
/// Forward transmuter: converts a validated read_file AgentToolCall into a FileRead.
/// </summary>
internal sealed class AgentToolCallToFileRead : ITransmuter<AgentToolCall, FileRead>
{
    /// <inheritdoc />
    public Task<FileRead> Transmute(AgentToolCall Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return FileSystemCompanionRouting.TryExtractPath(Input.ArgumentsJson, out string path, out string error)
            ? Task.FromResult(new FileRead(Input.CorrelationId, path))
            : throw new InvalidOperationException(
                $"Invariant violation: read_file call '{Input.CorrelationId}' reached {nameof(AgentToolCallToFileRead)} without a valid path. {error}");
    }
}
