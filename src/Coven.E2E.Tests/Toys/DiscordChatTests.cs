// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Discord;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the DiscordChat toy application.
/// Validates that Discord messages trigger responses via the virtual gateway.
/// </summary>
public sealed class DiscordChatTests
{
    private const ulong TestChannelId = 123456789UL;

    /// <summary>
    /// Creates a Discord branch manifest for simple echo tests (non-streaming).
    /// The standard Discord manifest declares ChatEfferentDraft and ChatChunk as consumed,
    /// which requires routes producing them. For simple echo tests, we use a minimal manifest.
    /// </summary>
    private static BranchManifest CreateEchoDiscordManifest() => new(
        Name: "DiscordChat",
        JournalEntryType: typeof(ChatEntry),
        Produces: new HashSet<Type> { typeof(ChatAfferent) },
        Consumes: new HashSet<Type> { typeof(ChatEfferent) },
        RequiredDaemons: [typeof(ContractDaemon)]);

    /// <summary>
    /// Tests that a simulated Discord message triggers an echo response.
    /// </summary>
    [Fact]
    public async Task SimulatedMessageTriggersResponse()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualDiscord()
            .ConfigureCoven(coven =>
            {
                DiscordClientConfig config = new()
                {
                    BotToken = "test-token",
                    ChannelId = TestChannelId
                };

                // Register Discord services directly
                coven.Services.AddDiscordChat(config);

                // Use minimal manifest without streaming types
                BranchManifest chat = CreateEchoDiscordManifest();

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        // Echo: incoming messages become outgoing
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - simulate a user message on Discord
        await host.Discord.SimulateUserMessageAsync(
            channelId: TestChannelId,
            author: "TestUser",
            content: "Hello from Discord!");

        // Give time for processing
        await Task.Delay(500);

        // Assert - verify response was sent
        Assert.NotEmpty(host.Discord.SentMessages);
        Assert.Contains(host.Discord.SentMessages, m =>
            m.ChannelId == TestChannelId &&
            m.Content == "Hello from Discord!");
    }

    /// <summary>
    /// Tests that bot messages are filtered out to prevent feedback loops.
    /// </summary>
    [Fact]
    public async Task BotMessageFiltered()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualDiscord()
            .ConfigureCoven(coven =>
            {
                DiscordClientConfig config = new()
                {
                    BotToken = "test-token",
                    ChannelId = TestChannelId
                };

                coven.Services.AddDiscordChat(config);
                BranchManifest chat = CreateEchoDiscordManifest();

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "Response: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - simulate a bot message (should be filtered)
        await host.Discord.SimulateMessageAsync(
            channelId: TestChannelId,
            author: "AnotherBot",
            content: "I am a bot message",
            isBot: true);

        // Give time for potential processing
        await Task.Delay(500);

        // Assert - no response should be sent for bot messages
        // The gateway filters out bot messages before they become ChatAfferent
        Assert.DoesNotContain(host.Discord.SentMessages, m =>
            m.Content.Contains("I am a bot message"));
    }

    /// <summary>
    /// Tests that messages from the wrong channel are ignored.
    /// </summary>
    [Fact]
    public async Task WrongChannelMessageIgnored()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualDiscord()
            .ConfigureCoven(coven =>
            {
                DiscordClientConfig config = new()
                {
                    BotToken = "test-token",
                    ChannelId = TestChannelId  // Only listen to this channel
                };

                coven.Services.AddDiscordChat(config);
                BranchManifest chat = CreateEchoDiscordManifest();

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "Echo: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - send message to a different channel
        await host.Discord.SimulateUserMessageAsync(
            channelId: 999999999UL,  // Different channel
            author: "TestUser",
            content: "Wrong channel message");

        await Task.Delay(500);

        // Assert - no response sent to the wrong channel
        Assert.DoesNotContain(host.Discord.SentMessages, m => m.ChannelId == 999999999UL);
    }

    /// <summary>
    /// Tests multiple messages processed in sequence.
    /// </summary>
    [Fact]
    public async Task MultipleMessagesProcessedInSequence()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualDiscord()
            .ConfigureCoven(coven =>
            {
                DiscordClientConfig config = new()
                {
                    BotToken = "test-token",
                    ChannelId = TestChannelId
                };

                coven.Services.AddDiscordChat(config);
                BranchManifest chat = CreateEchoDiscordManifest();

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - send multiple messages
        await host.Discord.SimulateUserMessageAsync(TestChannelId, "User1", "First message");
        await Task.Delay(200);
        await host.Discord.SimulateUserMessageAsync(TestChannelId, "User2", "Second message");
        await Task.Delay(200);
        await host.Discord.SimulateUserMessageAsync(TestChannelId, "User3", "Third message");
        await Task.Delay(500);

        // Assert - all messages should have responses
        Assert.True(host.Discord.SentMessages.Count >= 3,
            $"Expected at least 3 sent messages, got {host.Discord.SentMessages.Count}");

        Assert.Contains(host.Discord.SentMessages, m => m.Content == "First message");
        Assert.Contains(host.Discord.SentMessages, m => m.Content == "Second message");
        Assert.Contains(host.Discord.SentMessages, m => m.Content == "Third message");
    }

    /// <summary>
    /// Tests that the journal records Discord conversation.
    /// </summary>
    [Fact]
    public async Task ConversationRecordedInJournal()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualDiscord()
            .ConfigureCoven(coven =>
            {
                DiscordClientConfig config = new()
                {
                    BotToken = "test-token",
                    ChannelId = TestChannelId
                };

                coven.Services.AddDiscordChat(config);
                BranchManifest chat = CreateEchoDiscordManifest();

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "Reply: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act
        await host.Discord.SimulateUserMessageAsync(TestChannelId, "TestUser", "Journal test");
        await Task.Delay(500);

        // Assert - verify journal has entries
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();

        Assert.Contains(entries, e => e is ChatAfferent { Text: "Journal test" });
        Assert.Contains(entries, e => e is ChatEfferent { Text: "Reply: Journal test" });
    }

    /// <summary>
    /// Tests assertion helpers from the harness.
    /// </summary>
    [Fact]
    public async Task AssertionHelpersWorkCorrectly()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualDiscord()
            .ConfigureCoven(coven =>
            {
                DiscordClientConfig config = new()
                {
                    BotToken = "test-token",
                    ChannelId = TestChannelId
                };

                coven.Services.AddDiscordChat(config);
                BranchManifest chat = CreateEchoDiscordManifest();

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "Pong: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act
        await host.Discord.SimulateUserMessageAsync(TestChannelId, "User", "Ping");
        await Task.Delay(500);

        // Assert using harness assertion helpers
        host.Discord.AssertSentMessage(
            TestChannelId,
            content => content.Contains("Pong:"));

        host.Discord.AssertSentMessagesInOrder(
            m => m.ChannelId == TestChannelId && m.Content.StartsWith("Pong:", StringComparison.Ordinal));
    }
}
