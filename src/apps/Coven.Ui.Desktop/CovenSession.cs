// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Agents.Gemini;
using Coven.Agents.LLamaSharp;
using Coven.Agents.OpenAI;
using Coven.Chat;
using Coven.Chat.Ui;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Ui.Desktop.Local;
using Coven.Ui.Desktop.Logging;
using Coven.Ui.Desktop.Settings;
using Coven.Ui.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coven.Ui.Desktop;

/// <summary>
/// Owns one host built for a single provider selection, and the long-running ritual that
/// keeps its daemons alive.
/// </summary>
/// <remarks>
/// A session is bound to one provider. Agent leaves register their daemon additively against a
/// shared <c>AgentEntry</c> journal, so two providers in one coven would both answer every
/// prompt — switching provider therefore means building a new session, not reconfiguring this one.
/// </remarks>
internal sealed class CovenSession : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly CancellationTokenSource _cts = new();
    private readonly SessionContext _context;
    private readonly ClaudeClientConfig? _claudeConfig;
    private Task? _ritual;

    private CovenSession(
        IHost host,
        SessionContext context,
        ClaudeClientConfig? claudeConfig,
        AgentProvider provider,
        string? backendDescription)
    {
        _host = host;
        _context = context;
        _claudeConfig = claudeConfig;
        Provider = provider;
        BackendDescription = backendDescription;
    }

    /// <summary>Provider this session was built for.</summary>
    public AgentProvider Provider { get; }

    /// <summary>
    /// Which native backend local inference selected, or <see langword="null"/> for hosted
    /// providers. Worth surfacing: the difference between CUDA and CPU is the difference
    /// between a usable local model and one that appears hung.
    /// </summary>
    public string? BackendDescription { get; }

    /// <summary>
    /// Builds a session for the supplied settings. Covenant routes are validated here — a
    /// misconfigured combination throws <see cref="CovenantValidationException"/> with the
    /// exact route to add.
    /// </summary>
    public static CovenSession Create(AppSettings settings, UiChannel channel, SessionContext context)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(context);

        UiChatClientConfig uiConfig = new()
        {
            InputSender = "you",
            OutputSender = "assistant"
        };

        // Register the native backend preference before anything can touch LLamaSharp;
        // once its library loads the choice is frozen.
        string? backend = null;
        if (settings.Provider == AgentProvider.Local)
        {
            // Any error still recorded belongs to the previous model.
            LocalBackend.ForgetLastError();
            backend = LocalBackend.EnsureConfigured();
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // File logging, not console: this is a WinExe, so console output goes nowhere and
        // every diagnostic breadcrumb from the leaves would be lost.
        AppLog.Prepare();
        builder.Services.AddLogging(b => b
            .ClearProviders()
            .AddProvider(new FileLoggerProvider(AppLog.FilePath))
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning));

        // Registered before BuildCoven so the leaf's TryAddSingleton defers to these instances.
        builder.Services.AddSingleton<IUiChannel>(channel);
        builder.Services.AddSingleton(context);

        ClaudeClientConfig? claudeConfig = settings.Provider == AgentProvider.Anthropic
            ? new ClaudeClientConfig
            {
                ApiKey = settings.AnthropicApiKey,
                Model = settings.AnthropicModel,
                SystemPrompt = settings.SystemPrompt
            }
            : null;

        builder.Services.BuildCoven(coven =>
        {
            // Hosted providers emit AgentThought; a local GGUF has no separate reasoning
            // channel and its branch declares no such type, so the reasoning pane — and the
            // route that feeds it — only exist when the provider can actually supply one.
            bool hasReasoning = settings.Provider != AgentProvider.Local;

            BranchManifest chat = coven.UseUiChat(uiConfig, reg => reg.EnableStreaming());
            BranchManifest shell = coven.UseUiShell(reg =>
            {
                if (hasReasoning)
                {
                    reg.EnableReasoning();
                }
            });

            BranchManifest agents = settings.Provider switch
            {
                AgentProvider.Anthropic => coven.UseClaudeAgents(claudeConfig!, reg => reg.EnableStreaming()),
                AgentProvider.OpenAI => coven.UseOpenAIAgents(
                    new OpenAIClientConfig
                    {
                        ApiKey = settings.OpenAIApiKey,
                        Model = settings.OpenAIModel
                    },
                    reg => reg.EnableStreaming()),
                AgentProvider.Local => coven.UseLLamaSharpAgents(
                    new LLamaSharpClientConfig
                    {
                        ModelPath = settings.LocalModel,
                        SystemPrompt = settings.SystemPrompt,
                        // -1 offloads every layer to the GPU. Harmless on the CPU backend,
                        // which ignores it, and the difference between usable and unusable
                        // on the CUDA one.
                        GpuLayerCount = -1,
                        // The library default of 2048 is a few exchanges of chat before the
                        // conversation runs out of room, on models routinely trained for
                        // 32K or more. Raised to a size that holds a real conversation while
                        // keeping the KV cache modest — it is charged against the same memory
                        // budget as the weights.
                        ContextSize = 8192
                    },
                    reg => reg.EnableStreaming()),
                AgentProvider.Gemini => coven.UseGeminiAgents(
                    new GeminiClientConfig
                    {
                        ApiKey = settings.GeminiApiKey,
                        Model = settings.GeminiModel,
                        // Gemini calls the system prompt a system instruction.
                        SystemInstruction = settings.SystemPrompt
                    },
                    reg => reg.EnableStreaming()),
                _ => throw new NotSupportedException($"Unsupported provider: {settings.Provider}")
            };

            coven.Covenant()
                .Connect(chat)
                .Connect(agents)
                .Connect(shell)
                .Routes(c =>
                {
                    // UI → Agents: submitted messages become prompts.
                    c.Route<ChatAfferent, AgentPrompt>(
                        (msg, ct) => Task.FromResult(
                            new AgentPrompt(msg.Sender, msg.Text)));

                    // Agents → UI: windowed responses become finalized messages.
                    c.Route<AgentResponse, ChatEfferent>(
                        (r, ct) => Task.FromResult(
                            new ChatEfferent("assistant", r.Text)));

                    // Agents → UI: raw chunks render live, ahead of the finalized message.
                    c.Route<AgentAfferentChunk, ChatChunk>(
                        (chunk, ct) => Task.FromResult(
                            new ChatChunk("assistant", chunk.Text)));

                    if (hasReasoning)
                    {
                        // Agents → Shell: reasoning lands in its own journal, not the transcript.
                        c.Route<AgentThought, UiThought>(
                            (t, ct) => Task.FromResult(
                                new UiThought(t.Sender, t.Text)));

                        // Thought chunks are windowed into AgentThought; the raw stream is not shown.
                        c.Terminal<AgentAfferentThoughtChunk>();
                    }

                    // Notices are written directly by the application; nothing routes them onward.
                    c.Terminal<UiNotice>();
                });

            coven.MagikBlock<Empty, Empty, UiHostBlock>();
            coven.Done();
        });

        return new CovenSession(builder.Build(), context, claudeConfig, settings.Provider, backend);
    }

    /// <summary>
    /// Starts the ritual on a background task. Daemons auto-start on scope entry.
    /// </summary>
    public void Start()
    {
        _ritual = RunAsync();
    }

    /// <summary>
    /// Changes the model without rebuilding, when the provider's configuration allows it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ClaudeClientConfig"/> exposes settable properties and its gateway reads
    /// <c>Model</c> when building each request, so a mutation takes effect on the next turn.
    /// <see cref="OpenAIClientConfig"/> is a record with <c>init</c> properties and cannot be
    /// changed after registration.
    /// </para>
    /// <para>
    /// This matters more than a latency optimisation: journal hydration is not implemented, so
    /// a rebuild discards the conversation. The hot path is what lets a user change model
    /// mid-conversation and keep their transcript.
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> when the change was applied in place.</returns>
    public bool TryApplyModel(string model)
    {
        if (_claudeConfig is null || string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        _claudeConfig.Model = model;
        return true;
    }

    /// <summary>Writes an application notice to the shell journal.</summary>
    public async Task NoticeAsync(UiNoticeLevel level, string text, CancellationToken cancellationToken = default)
    {
        IScrivener<UiEntry>? journal = _host.Services.GetService<IScrivener<UiEntry>>();
        if (journal is null)
        {
            return;
        }

        await journal.WriteAsync(new UiNotice(level, text), cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        try
        {
            ICoven coven = _host.Services.GetRequiredService<ICoven>();
            await coven.Ritual<Empty, Empty>(new Empty(), _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cooperative shutdown.
        }
        catch (Exception ex)
        {
            _context.Fail(ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _context.Clear();
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_ritual is not null)
        {
            try
            {
                await _ritual.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Shutdown is best-effort; a stuck or failed daemon must not block a rebuild.
            }
        }

        _cts.Dispose();
        _host.Dispose();
    }
}
