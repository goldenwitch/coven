// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Agents.Gemini;
using Coven.Chat;
using Coven.Chat.Ui;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Coven.Testing.Harness;
using Coven.Ui.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.E2E.Tests.Ui;

/// <summary>
/// Tests that a gateway failure becomes a reported daemon failure instead of a silent stall.
/// </summary>
/// <remarks>
/// Regression coverage for a real hang: <c>ClaudeAgentSession.Completion</c> used
/// <see cref="Task.WhenAll(Task[])"/> across three pumps, two of which tail a journal and only
/// end on cancellation. When the gateway pump faulted on an API error, <c>WhenAll</c> waited
/// forever for the other two, the daemon never failed, and the caller sat on a dead turn with
/// no indication anything had gone wrong.
/// </remarks>
public sealed class GatewayFailureTests
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
    /// With no scripted response, the virtual gateway throws — standing in for any API error,
    /// such as an authentication or billing rejection. The failure must reach the daemon
    /// journal promptly rather than hanging.
    /// </summary>
    [Fact]
    public async Task GatewayErrorSurfacesAsADaemonFailure()
    {
        UiChannel channel = new();

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

        // Deliberately script nothing: the gateway throws when a prompt arrives.
        await host.StartAsync();

        await channel.SubmitAsync("does this work?");

        DaemonEvent failure = await WaitForFailureAsync(host, TimeSpan.FromSeconds(15));

        Assert.Equal("FailureOccurred", failure.GetType().Name);
    }

    // FailureOccurred is internal to Coven.Core, so match on the event's type name.
    /// <summary>
    /// Same guarantee for Gemini, which had no session supervision at all before it was added
    /// to the desktop application — a faulted pump was swallowed outright.
    /// </summary>
    [Fact]
    public async Task GeminiGatewayErrorSurfacesAsADaemonFailure()
    {
        UiChannel channel = new();

        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualGemini()
            .ConfigureServices(services => services.AddSingleton<IUiChannel>(channel))
            .ConfigureCoven(coven =>
            {
                BranchManifest chat = coven.UseUiChat(UiConfig);
                BranchManifest agents = coven.UseGeminiAgents(new GeminiClientConfig
                {
                    ApiKey = "test-key",
                    Model = "gemini-2.0-flash"
                });
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

        // Deliberately script nothing: the gateway throws when a prompt arrives.
        await host.StartAsync();

        await channel.SubmitAsync("does this work?");

        DaemonEvent failure = await WaitForFailureAsync(host, TimeSpan.FromSeconds(15));

        Assert.Equal("FailureOccurred", failure.GetType().Name);
    }

    private static async Task<DaemonEvent> WaitForFailureAsync(E2ETestHost host, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            IReadOnlyList<DaemonEvent> events = await host.Journals.GetEntriesAsync<DaemonEvent>();
            DaemonEvent? failure = events.FirstOrDefault(
                e => string.Equals(e.GetType().Name, "FailureOccurred", StringComparison.Ordinal));

            if (failure is not null)
            {
                return failure;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"No daemon failure was reported within {timeout}. "
            + "A faulted gateway pump is being swallowed, so callers will wait forever.");
    }
}
