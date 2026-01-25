// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the ConsoleChat toy application.
/// Validates that the declarative covenant correctly routes chat messages.
/// </summary>
public sealed class ConsoleChatTests
{
    /// <summary>
    /// Tests that user input is echoed back with "Echo: " prefix.
    /// This mirrors the behavior in Coven.Toys.ConsoleChat.
    /// </summary>
    [Fact]
    public async Task UserInputEchoedWithPrefix()
    {
        // Arrange - configure host with virtual console
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
                        // Echo: incoming messages become outgoing with "Echo: " prefix
                        c.Route<ChatAfferent, ChatEfferent>(
                            (msg, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "Echo: " + msg.Text)));
                    });
            })
            .Build();

        // Start the host
        await host.StartAsync();

        // Act - send user input
        await host.Console.SendInputAsync("Hello, World!");

        // Assert - verify echoed output
        string output = await host.Console.WaitForOutputContainingAsync(
            "Echo: Hello, World!",
            TimeSpan.FromSeconds(5));

        Assert.Contains("Echo: Hello, World!", output);
    }

    /// <summary>
    /// Tests handling of empty input strings.
    /// Empty/whitespace strings are filtered by the console gateway - they produce no output.
    /// </summary>
    [Fact]
    public async Task EmptyInputIsFiltered()
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
                                new ChatEfferent("BOT", "Echo: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - send empty input followed by a real message
        await host.Console.SendInputAsync("");
        await host.Console.SendInputAsync("   "); // whitespace only
        await host.Console.SendInputAsync("Hello"); // real message

        // Assert - only the real message should be echoed
        string output = await host.Console.WaitForOutputContainingAsync(
            "Echo: Hello",
            TimeSpan.FromSeconds(5));

        Assert.Equal("Echo: Hello", output);

        // Verify no other output (empty strings were filtered)
        IReadOnlyList<string> allOutput = host.Console.DrainOutput();
        Assert.Empty(allOutput); // No additional output
    }

    /// <summary>
    /// Tests multiple messages in sequence are all echoed correctly.
    /// </summary>
    [Fact]
    public async Task MultipleMessagesAllEchoedInSequence()
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
                                new ChatEfferent("BOT", "Echo: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - send multiple messages
        await host.Console.SendInputAsync("First message");
        await host.Console.SendInputAsync("Second message");
        await host.Console.SendInputAsync("Third message");

        // Assert - collect all outputs
        IReadOnlyList<string> outputs = await host.Console.CollectOutputAsync(
            3,
            TimeSpan.FromSeconds(10));

        Assert.Equal("Echo: First message", outputs[0]);
        Assert.Equal("Echo: Second message", outputs[1]);
        Assert.Equal("Echo: Third message", outputs[2]);
    }

    /// <summary>
    /// Tests that special characters in input are preserved in echo.
    /// </summary>
    [Fact]
    public async Task SpecialCharactersPreservedInEcho()
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
                                new ChatEfferent("BOT", "Echo: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act - send message with special characters
        string specialMessage = "Hello! @user #channel $money 100% <tag> \"quoted\"";
        await host.Console.SendInputAsync(specialMessage);

        // Assert
        string output = await host.Console.WaitForOutputAsync(TimeSpan.FromSeconds(5));

        Assert.Equal($"Echo: {specialMessage}", output);
    }

    /// <summary>
    /// Tests that the journal records the conversation correctly.
    /// </summary>
    [Fact]
    public async Task ConversationRecordedInJournal()
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
                                new ChatEfferent("BOT", "Echo: " + msg.Text)));
                    });
            })
            .Build();

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Test message");
        await host.Console.WaitForOutputContainingAsync("Echo:", TimeSpan.FromSeconds(5));

        // Assert - verify journal has entries
        IReadOnlyList<ChatEntry> entries = await host.Journals.GetEntriesAsync<ChatEntry>();

        // Should have at least the afferent (incoming) and efferent (outgoing) entries
        Assert.Contains(entries, e => e is ChatAfferent { Text: "Test message" });
        Assert.Contains(entries, e => e is ChatEfferent { Text: "Echo: Test message" });
    }
}
