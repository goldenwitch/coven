// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Claude;
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
/// The UI chat leaf's obligations toward its journal and its own pumps: an entry is durable
/// before anyone renders it, and every pump that can fail is supervised.
/// </summary>
public sealed class UiChatSupervisionTests
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
    /// A user interface that throws while rendering must not take the entry with it. The
    /// scrivener appends before it publishes, so the payload stays durable and replayable
    /// however badly the interface behaves.
    /// </summary>
    [Fact]
    public async Task AnEntryIsJournaledEvenWhenTheInterfaceThrows()
    {
        HostileUiChannel channel = new(throwOnPublish: true);

        await using E2ETestHost host = BuildHost(channel);
        host.Claude.EnqueueResponse("A response the interface will refuse to render.");
        await host.StartAsync();

        await channel.SubmitAsync("hello");

        UiChatEntry entry = await WaitForEntryAsync<UiChatEfferent>(host, TimeSpan.FromSeconds(15));

        Assert.Contains(
            "A response the interface will refuse to render.",
            ((UiChatEfferent)entry).Text,
            StringComparison.Ordinal);

        // The publish really did throw; the durability above is not a false positive.
        Assert.True(channel.PublishAttempted);
    }

    /// <summary>
    /// A fault in the input path is reported while the session is running, not held until
    /// shutdown. The input pump carries the user's own messages and can fail on its own, so
    /// leaving it out of supervision means the interface accepts messages that go nowhere.
    /// </summary>
    [Fact]
    public async Task AFaultInTheInputPumpIsReportedAsADaemonFailure()
    {
        HostileUiChannel channel = new(throwOnRead: true);

        await using E2ETestHost host = BuildHost(channel);
        host.Claude.EnqueueResponse("first turn is fine");
        await host.StartAsync();

        // The first read succeeds and the session reaches Running; the next one faults.
        await channel.SubmitAsync("hello");

        DaemonEvent failure = await WaitForFailureAsync(host, TimeSpan.FromSeconds(15));

        Assert.Equal("FailureOccurred", failure.GetType().Name);
    }

    private static E2ETestHost BuildHost(IUiChannel channel) =>
        new E2ETestHostBuilder()
            .UseVirtualClaude()
            .ConfigureServices(services => services.AddSingleton(channel))
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

    private static async Task<UiChatEntry> WaitForEntryAsync<TEntry>(E2ETestHost host, TimeSpan timeout)
        where TEntry : UiChatEntry
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            IReadOnlyList<UiChatEntry> entries = await host.Journals.GetEntriesAsync<UiChatEntry>();
            TEntry? found = entries.OfType<TEntry>().FirstOrDefault();
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"No {typeof(TEntry).Name} reached the journal within {timeout}. "
            + "An entry the interface refused to render was lost instead of being appended.");
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
            + "A faulted input pump is being swallowed until shutdown.");
    }

    /// <summary>A channel that misbehaves in one specific way, to order.</summary>
    private sealed class HostileUiChannel(bool throwOnPublish = false, bool throwOnRead = false) : IUiChannel
    {
        private readonly UiChannel _inner = new();
        private int _reads;

        public bool PublishAttempted { get; private set; }

        public ValueTask SubmitAsync(string text, CancellationToken cancellationToken = default) =>
            _inner.SubmitAsync(text, cancellationToken);

        public ValueTask<string?> ReadInputAsync(CancellationToken cancellationToken = default)
        {
            // The first read always succeeds, so the session reaches Running before anything
            // breaks. Failing on the very first one would make this a startup failure, which
            // is a different case and already reported by scope activation.
            if (throwOnRead && Interlocked.Increment(ref _reads) > 1)
            {
                throw new InvalidOperationException("the input path is broken");
            }

            return _inner.ReadInputAsync(cancellationToken);
        }

        public ValueTask PublishAsync(UiOutbound outbound, CancellationToken cancellationToken = default)
        {
            PublishAttempted = true;

            if (throwOnPublish)
            {
                throw new InvalidOperationException("the interface refused to render");
            }

            return _inner.PublishAsync(outbound, cancellationToken);
        }
    }
}
