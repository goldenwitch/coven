// SPDX-License-Identifier: BUSL-1.1

using Coven.FileSystem;
using Coven.Transmutation;

namespace Coven.Agents.FileSystem;

/// <summary>
/// Return transmuter: converts FileFailure → AgentToolFailure.
/// </summary>
internal sealed class FileFailureToAgentToolFailure : ITransmuter<FileFailure, AgentToolFailure>
{
    /// <inheritdoc />
    public Task<AgentToolFailure> Transmute(FileFailure Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AgentToolFailure(
            FileSystemCompanionRouting.Sender,
            Input.CorrelationId,
            $"Tool '{FileSystemCompanionRouting.ReadFileToolName}' call '{Input.CorrelationId}' failed: {Input.FailureKind}: {Input.Message}"));
    }
}
