// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.LLamaSharp;
using Coven.Chat;
using Coven.Chat.Ui;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Ui.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.E2E.Tests.Ui;

/// <summary>
/// Covenant-shape tests for the desktop application's local provider.
/// </summary>
/// <remarks>
/// <para>
/// Validation runs during <c>BuildCoven</c>, before any daemon starts, so these build the real
/// covenant without loading a model. The model path is never opened.
/// </para>
/// <para>
/// Regression coverage: the local branch produces no <see cref="AgentThought"/>, because a GGUF
/// model has no separate reasoning channel. The application originally routed reasoning
/// unconditionally and selecting the local provider failed at ritual start with a bare
/// <c>KeyNotFoundException</c>.
/// </para>
/// </remarks>
public sealed class LocalProviderCovenantTests
{
    private static readonly UiChatClientConfig UiConfig = new()
    {
        InputSender = "you",
        OutputSender = "assistant"
    };

    private static LLamaSharpClientConfig LocalConfig => new()
    {
        ModelPath = Path.Combine(Path.GetTempPath(), "not-loaded.gguf"),
        GpuLayerCount = -1
    };

    /// <summary>
    /// The covenant the application builds for the local provider — chat and streaming, no
    /// reasoning — validates.
    /// </summary>
    [Fact]
    public void LocalProviderCovenantValidates()
    {
        ServiceCollection services = new();

        services.BuildCoven(coven =>
        {
            BranchManifest chat = coven.UseUiChat(UiConfig, reg => reg.EnableStreaming());
            BranchManifest shell = coven.UseUiShell();
            BranchManifest agents = coven.UseLLamaSharpAgents(LocalConfig, reg => reg.EnableStreaming());

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

                    c.Terminal<UiNotice>();
                });
        });
    }

    /// <summary>
    /// Routing reasoning from a local model is rejected at build time, naming
    /// <see cref="AgentThought"/> and what to do about it.
    /// </summary>
    [Fact]
    public void RoutingReasoningFromALocalModelFailsValidation()
    {
        ServiceCollection services = new();

        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                BranchManifest chat = coven.UseUiChat(UiConfig, reg => reg.EnableStreaming());
                BranchManifest shell = coven.UseUiShell(reg => reg.EnableReasoning());
                BranchManifest agents = coven.UseLLamaSharpAgents(LocalConfig, reg => reg.EnableStreaming());

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

                        // No connected branch produces AgentThought.
                        c.Route<AgentThought, UiThought>(
                            (t, ct) => Task.FromResult(new UiThought(t.Sender, t.Text)));

                        c.Terminal<UiNotice>();
                    });
            });
        });

        Assert.Contains(nameof(AgentThought), exception.Message, StringComparison.Ordinal);
        Assert.Contains("no connected branch declares it", exception.Message, StringComparison.Ordinal);
    }
}
