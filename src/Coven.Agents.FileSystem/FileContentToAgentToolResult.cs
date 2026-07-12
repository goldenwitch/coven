// SPDX-License-Identifier: BUSL-1.1

using Coven.FileSystem;
using Coven.Transmutation;

namespace Coven.Agents.FileSystem;

/// <summary>
/// Return transmuter: converts FileContent → AgentToolResult.
/// </summary>
internal sealed class FileContentToAgentToolResult : ITransmuter<FileContent, AgentToolResult>
{
    /// <inheritdoc />
    public Task<AgentToolResult> Transmute(FileContent Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AgentToolResult(FileSystemCompanionRouting.Sender, Input.CorrelationId, Input.Content));
    }
}
