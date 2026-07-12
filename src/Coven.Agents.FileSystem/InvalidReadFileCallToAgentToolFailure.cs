// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.FileSystem;

/// <summary>
/// Converts an invalid read_file tool call into an agent-visible failure instead of faulting the covenant pump.
/// </summary>
internal sealed class InvalidReadFileCallToAgentToolFailure : ITransmuter<AgentToolCall, AgentToolFailure>
{
    /// <inheritdoc />
    public Task<AgentToolFailure> Transmute(AgentToolCall Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new AgentToolFailure(
            FileSystemCompanionRouting.Sender,
            Input.CorrelationId,
            FileSystemCompanionRouting.BuildInvalidReadFileMessage(Input)));
    }
}