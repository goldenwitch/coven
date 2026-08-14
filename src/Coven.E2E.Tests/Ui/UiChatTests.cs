// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent;
using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Chat;
using Coven.Chat.Ui;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Ui.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.E2E.Tests.Ui;

/// <summary>
/// E2E tests for the UI chat leaf. Validates that messages submitted through
/// <see cref="UiChannel"/> reach the agent and that responses come back out.
/// </summary>
public sealed class UiChatTests
{
    private static readonly UiChatClientConfig UiConfig = new()
    {
        InputSender = "you",
        OutputSender = "assistant"
    };

    private static ClaudeClientConfig ClaudeConfig => new()
    {
        ApiKey = "test-key",
        Model = "claude-sonnet-4-20250514"
    };

    /// <summary>
    /// A message submitted through the channel reaches the gateway, and the scripted
    /// response is published back as a finalized message.
    /// </summary>
    [Fact]
    public async Task SubmittedMessageRoundTripsBackToTheChannel()
    {
        UiChannel channel = new();
        ConcurrentQueue<UiOutbound> received = new();
        channel.Outbound += received.Enqueue;

        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualClaude()
            .ConfigureServices(services => services.AddSingleton<IUiChannel>(channel))
            .ConfigureCoven(coven =>
            {
                BranchManifest chat = coven.UseUiChat(UiConfig);
                BranchManifest agents = coven.UseClaudeAgents(ClaudeConfig);
                BranchManifest shell = coven.UseUiShell(reg => reg.EnableReasoning());

                coven.Covenant()
                    .Connect(chat)
                    .Connect(agents)
                    .Connect(shell)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, AgentPrompt>(
                            (msg, ct) => Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));

                        c.Route<AgentResponse, ChatEfferent>(
                            (r, ct) => Task.FromResult(new ChatEfferent("assistant", r.Text)));

                        c.Route<AgentThought, UiThought>(
                            (t, ct) => Task.FromResult(new UiThought(t.Sender, t.Text)));

                        c.Terminal<UiNotice>();
                    });
            })
            .Build();

        host.Claude.EnqueueResponse("Hello from the agent.");

        await host.StartAsync();

        await channel.SubmitAsync("Hello, agent!");

        UiOutbound message = await WaitForAsync(
            received,
            o => o.Kind == UiOutboundKind.Message && o.Text.Contains("Hello from the agent.", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10));

        Assert.Equal("assistant", message.Sender);

        // The prompt reached the gateway with the user's text intact.
        Assert.Single(host.Claude.SentMessages);
        Assert.Contains("Hello, agent!", host.Claude.SentMessages[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// With streaming enabled, chunks are published incrementally before the
    /// windowed response arrives as a finalized message.
    /// </summary>
    [Fact]
    public async Task StreamingChunksArriveBeforeTheFinalizedMessage()
    {
        UiChannel channel = new();
        ConcurrentQueue<UiOutbound> received = new();
        channel.Outbound += received.Enqueue;

        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualClaude()
            .ConfigureServices(services => services.AddSingleton<IUiChannel>(channel))
            .ConfigureCoven(coven =>
            {
                BranchManifest chat = coven.UseUiChat(UiConfig, reg => reg.EnableStreaming());
                BranchManifest agents = coven.UseClaudeAgents(ClaudeConfig, reg => reg.EnableStreaming());
                BranchManifest shell = coven.UseUiShell(reg => reg.EnableReasoning());

                coven.Covenant()
                    .Connect(chat)
                    .Connect(agents)
                    .Connect(shell)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, AgentPrompt>(
                            (msg, ct) => Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));

                        c.Route<AgentResponse, ChatEfferent>(
                            (r, ct) => Task.FromResult(new ChatEfferent("assistant", r.Text)));

                        c.Route<AgentAfferentChunk, ChatChunk>(
                            (chunk, ct) => Task.FromResult(new ChatChunk("assistant", chunk.Text)));

                        c.Route<AgentThought, UiThought>(
                            (t, ct) => Task.FromResult(new UiThought(t.Sender, t.Text)));

                        c.Terminal<AgentAfferentThoughtChunk>();
                        c.Terminal<UiNotice>();
                    });
            })
            .Build();

        host.Claude.EnqueueStreamingResponse(["Streaming ", "in ", "pieces."]);

        await host.StartAsync();

        await channel.SubmitAsync("Stream something.");

        UiOutbound finalized = await WaitForAsync(
            received,
            o => o.Kind == UiOutboundKind.Message,
            TimeSpan.FromSeconds(10));

        List<UiOutbound> chunks = [.. received.Where(o => o.Kind == UiOutboundKind.Chunk)];

        Assert.NotEmpty(chunks);
        Assert.Contains("Streaming", string.Concat(chunks.Select(c => c.Text)), StringComparison.Ordinal);
        Assert.Contains("pieces.", finalized.Text, StringComparison.Ordinal);
    }

    private static async Task<UiOutbound> WaitForAsync(
        ConcurrentQueue<UiOutbound> received,
        Func<UiOutbound, bool> predicate,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            UiOutbound? match = received.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(25);
        }

        string seen = string.Join(
            ", ",
            received.Select(o => $"{o.Kind}:'{o.Text}'"));
        throw new TimeoutException($"No matching outbound within {timeout}. Received: [{seen}]");
    }
}
