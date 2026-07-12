// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Core.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Coven.Core.Tests.Claude;

public class ClaudeAgentSessionTests
{
    private sealed class NoOpClaudeShatterPolicy : IShatterPolicy<ClaudeEntry>
    {
        public IEnumerable<ClaudeEntry> Shatter(ClaudeEntry input) => [];
    }

    private sealed class ToolLoopGateway(IScrivener<ClaudeEntry> journal) : IClaudeGatewayConnection
    {
        private readonly IScrivener<ClaudeEntry> _journal = journal;

        public Task ConnectAsync() => Task.CompletedTask;

        public async Task SendAsync(ClaudeEfferent outgoing, long outgoingPosition, CancellationToken cancellationToken)
        {
            long toolUsePosition = await _journal.WriteAsync(new ClaudeToolUse(
                Sender: "claude",
                ToolUseId: "tool-1",
                ToolName: "read_file",
                ArgumentsJson: "{\"path\":\"README.md\"}",
                MessageId: "message-1",
                Timestamp: DateTimeOffset.UtcNow,
                Model: "claude-test"), cancellationToken).ConfigureAwait(false);

            (_, ClaudeToolResult result) = await _journal.WaitForAsync<ClaudeToolResult>(
                toolUsePosition - 1,
                entry => entry.ToolUseId == "tool-1",
                cancellationToken).ConfigureAwait(false);

            await _journal.WriteAsync(new ClaudeAfferent(
                Sender: "claude",
                Text: $"tool result: {result.Result}",
                MessageId: "message-1",
                Timestamp: DateTimeOffset.UtcNow,
                Model: "claude-test"), cancellationToken).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task ToolUseRoundTripDoesNotDeadlockAgentsToClaudePump()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        InMemoryScrivener<ClaudeEntry> innerClaudeJournal = new();
        ToolLoopGateway gateway = new(innerClaudeJournal);
        ClaudeScrivener claudeJournal = new(innerClaudeJournal, NullLogger<ClaudeScrivener>.Instance);
        InMemoryScrivener<AgentEntry> agentJournal = new();
        ClaudeTransmuter transmuter = new();

        await using ClaudeAgentSession session = new(
            gateway,
            claudeJournal,
            agentJournal,
            new NoOpClaudeShatterPolicy(),
            transmuter,
            transmuter,
            NullLogger<ClaudeAgentSession>.Instance,
            cts.Token);

        await session.StartAsync();
        await agentJournal.WriteAsync(new AgentPrompt("user", "read README"), cts.Token);

        (_, AgentToolCall toolCall) = await agentJournal.WaitForAsync<AgentToolCall>(
            0,
            entry => entry.ToolName == "read_file",
            cts.Token);

        await agentJournal.WriteAsync(new AgentToolResult("filesystem", toolCall.CorrelationId, "README contents"), cts.Token);

        (_, AgentResponse response) = await agentJournal.WaitForAsync<AgentResponse>(
            0,
            entry => entry.Text.Contains("README contents", StringComparison.Ordinal),
            cts.Token);

        Assert.Equal("claude", response.Sender);
        Assert.Equal("tool result: README contents", response.Text);
    }
}