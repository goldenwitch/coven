// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the FileScrivenerConsole toy application.
/// Validates journal persistence with file scrivener (using in-memory replacement for testing).
/// </summary>
public sealed class FileScrivenerConsoleTests
{
    /// <summary>
    /// Tests that journal entries are recorded and can be read back.
    /// Uses the default InMemoryScrivener registered by the console chat.
    /// </summary>
    [Fact]
    public async Task JournalEntriesRecorded()
    {
        // Arrange - configure similar to FileScrivenerConsole but without file persistence
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig config = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                BranchManifest chat = coven.UseConsoleChat(config);

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

        // Act - send multiple messages
        await host.Console.SendInputAsync("First entry");
        await host.Console.WaitForOutputContainingAsync("Echo: First", TimeSpan.FromSeconds(5));

        await host.Console.SendInputAsync("Second entry");
        await host.Console.WaitForOutputContainingAsync("Echo: Second", TimeSpan.FromSeconds(5));

        // Assert - verify journal has all entries
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();

        // Should have at least 4 entries: 2 afferent + 2 efferent
        Assert.True(entries.Count >= 4, $"Expected at least 4 entries, got {entries.Count}");

        // Verify specific entries exist
        Assert.Contains(entries, e => e is ChatAfferent { Text: "First entry" });
        Assert.Contains(entries, e => e is ChatAfferent { Text: "Second entry" });
        Assert.Contains(entries, e => e is ChatEfferent { Text: "Echo: First entry" });
        Assert.Contains(entries, e => e is ChatEfferent { Text: "Echo: Second entry" });
    }

    /// <summary>
    /// Tests that entries are recorded in the correct order.
    /// </summary>
    [Fact]
    public async Task JournalEntriesCorrectOrder()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig config = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                BranchManifest chat = coven.UseConsoleChat(config);

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "Response to: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - send messages and wait for responses
        await host.Console.SendInputAsync("Message A");
        await host.Console.WaitForOutputContainingAsync("Response to: Message A", TimeSpan.FromSeconds(5));

        await host.Console.SendInputAsync("Message B");
        await host.Console.WaitForOutputContainingAsync("Response to: Message B", TimeSpan.FromSeconds(5));

        // Assert - entries should be in order
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();

        // Filter to just afferent messages
        List<ChatAfferent> afferents = [.. entries.OfType<ChatAfferent>()];
        Assert.True(afferents.Count >= 2, "Expected at least 2 afferent entries");

        // First afferent should be "Message A"
        int indexA = afferents.FindIndex(e => e.Text == "Message A");
        int indexB = afferents.FindIndex(e => e.Text == "Message B");

        Assert.True(indexA >= 0, "Message A not found");
        Assert.True(indexB >= 0, "Message B not found");
        Assert.True(indexA < indexB, "Message A should appear before Message B");
    }

    /// <summary>
    /// Tests that the efferent response follows its corresponding afferent.
    /// </summary>
    [Fact]
    public async Task ResponseFollowsRequestInJournal()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig config = new()
                {
                    InputSender = "user",
                    OutputSender = "bot"
                };

                BranchManifest chat = coven.UseConsoleChat(config);

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("bot", "Reply: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Test input");
        await host.Console.WaitForOutputContainingAsync("Reply:", TimeSpan.FromSeconds(5));

        // Assert
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();

        // Find the request and response
        int requestIndex = entries.ToList().FindIndex(e => e is ChatAfferent { Text: "Test input" });
        int responseIndex = entries.ToList().FindIndex(e => e is ChatEfferent { Text: "Reply: Test input" });

        Assert.True(requestIndex >= 0, "Request not found in journal");
        Assert.True(responseIndex >= 0, "Response not found in journal");
        Assert.True(requestIndex < responseIndex, "Response should follow request in journal");
    }

    /// <summary>
    /// Tests WithInMemoryScrivener replaces file scrivener.
    /// </summary>
    [Fact]
    public async Task WithInMemoryScrivenerReplacesFileScrivener()
    {
        // Arrange - explicitly use WithInMemoryScrivener
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .WithInMemoryScrivener<ChatEntry>()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig config = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                BranchManifest chat = coven.UseConsoleChat(config);

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

        // Act
        await host.Console.SendInputAsync("Memory test");
        await host.Console.WaitForOutputContainingAsync("Echo:", TimeSpan.FromSeconds(5));

        // Assert - entries should be accessible via in-memory scrivener
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e is ChatAfferent { Text: "Memory test" });
    }

    /// <summary>
    /// Tests that sender labels are preserved in journal entries.
    /// </summary>
    [Fact]
    public async Task SenderLabelsPreservedInJournal()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig config = new()
                {
                    InputSender = "human",
                    OutputSender = "assistant"
                };

                BranchManifest chat = coven.UseConsoleChat(config);

                coven.Covenant()
                    .Connect(chat)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("assistant", "Noted: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Check sender labels");
        await host.Console.WaitForOutputContainingAsync("Noted:", TimeSpan.FromSeconds(5));

        // Assert
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();

        // Verify sender labels are preserved
        ChatAfferent? afferent = entries.OfType<ChatAfferent>().FirstOrDefault(e => e.Text == "Check sender labels");
        Assert.NotNull(afferent);
        Assert.Equal("human", afferent.Sender);

        ChatEfferent? efferent = entries.OfType<ChatEfferent>().FirstOrDefault(e => e.Text.Contains("Noted:"));
        Assert.NotNull(efferent);
        Assert.Equal("assistant", efferent.Sender);
    }
}
