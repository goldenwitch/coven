// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Gemini;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the ConsoleGemini toy application.
/// Validates that chat messages are routed to the Gemini gateway and responses appear on console.
/// </summary>
public sealed class ConsoleGeminiTests
{
    /// <summary>
    /// Tests that a user message is sent to the Gemini gateway and the scripted response appears on console.
    /// </summary>
    [Fact]
    public async Task UserMessageSentToGeminiResponseAppearsOnConsole()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualGemini()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                GeminiClientConfig geminiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gemini-2.0-flash"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig);

                coven.Covenant()
                    .Connect(chat)
                    .Connect(agents)
                    .Routes(c =>
                    {
                        // Chat → Agents: incoming messages become prompts
                        c.Route<ChatAfferent, AgentPrompt>(
                            (msg, ct) => Task.FromResult(
                                new AgentPrompt(msg.Sender, msg.Text)));

                        // Agents → Chat: responses become outgoing messages
                        c.Route<AgentResponse, ChatEfferent>(
                            (r, ct) => Task.FromResult(
                                new ChatEfferent("BOT", r.Text)));

                        // Thoughts → Chat for visibility
                        c.Route<AgentThought, ChatEfferent>(
                            (t, ct) => Task.FromResult(
                                new ChatEfferent("BOT", t.Text)));
                    });
            })
            .Build();

        // Enqueue scripted response before starting
        host.Gemini.EnqueueResponse("Hello! I am an AI assistant.");

        await host.StartAsync();

        // Act - send user message
        await host.Console.SendInputAsync("Hello, AI!");

        // Assert - verify response appears on console
        string output = await host.Console.WaitForOutputContainingAsync(
            "I am an AI assistant",
            TimeSpan.FromSeconds(10));

        Assert.Contains("I am an AI assistant", output);
    }

    /// <summary>
    /// Tests that the user message is correctly captured by the Gemini gateway.
    /// </summary>
    [Fact]
    public async Task UserMessageCapturedByGateway()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualGemini()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                GeminiClientConfig geminiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gemini-2.0-flash"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig);

                coven.Covenant()
                    .Connect(chat)
                    .Connect(agents)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, AgentPrompt>(
                            (msg, ct) => Task.FromResult(
                                new AgentPrompt(msg.Sender, msg.Text)));

                        c.Route<AgentResponse, ChatEfferent>(
                            (r, ct) => Task.FromResult(
                                new ChatEfferent("BOT", r.Text)));

                        // Mark AgentThought as terminal since we don't display it
                        c.Terminal<AgentThought>();
                    });
            })
            .Build();

        host.Gemini.EnqueueResponse("Response from AI");

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the meaning of life?");

        // Wait for response to ensure message was processed
        await host.Console.WaitForOutputContainingAsync("Response from AI", TimeSpan.FromSeconds(10));

        // Assert - verify the gateway captured the message
        Assert.Single(host.Gemini.SentMessages);
        Assert.Contains("What is the meaning of life?", host.Gemini.SentMessages[0].Text);
    }

    /// <summary>
    /// Tests that the journal records the full conversation flow.
    /// </summary>
    [Fact]
    public async Task ConversationRecordedInJournal()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualGemini()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                GeminiClientConfig geminiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gemini-2.0-flash"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig);

                coven.Covenant()
                    .Connect(chat)
                    .Connect(agents)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, AgentPrompt>(
                            (msg, ct) => Task.FromResult(
                                new AgentPrompt(msg.Sender, msg.Text)));

                        c.Route<AgentResponse, ChatEfferent>(
                            (r, ct) => Task.FromResult(
                                new ChatEfferent("BOT", r.Text)));

                        c.Terminal<AgentThought>();
                    });
            })
            .Build();

        host.Gemini.EnqueueResponse("AI response text");

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("User prompt text");
        await host.Console.WaitForOutputContainingAsync("AI response text", TimeSpan.FromSeconds(10));

        // Assert - verify chat journal has conversation
        IReadOnlyList<ChatEntry> chatEntries = await host.Journals.GetEntriesAsync<ChatEntry>();
        Assert.Contains(chatEntries, e => e is ChatAfferent { Text: "User prompt text" });
        Assert.Contains(chatEntries, e => e is ChatEfferent { Text: "AI response text" });

        // Assert - verify agent journal has prompt and response
        IReadOnlyList<AgentEntry> agentEntries = await host.Journals.GetEntriesAsync<AgentEntry>();
        Assert.Contains(agentEntries, e => e is AgentPrompt { Text: "User prompt text" });
        Assert.Contains(agentEntries, e => e is AgentResponse { Text: "AI response text" });
    }

    /// <summary>
    /// Tests multiple message exchange in sequence.
    /// </summary>
    [Fact]
    public async Task MultipleExchangesProcessedCorrectly()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualGemini()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                GeminiClientConfig geminiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gemini-2.0-flash"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig);

                coven.Covenant()
                    .Connect(chat)
                    .Connect(agents)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, AgentPrompt>(
                            (msg, ct) => Task.FromResult(
                                new AgentPrompt(msg.Sender, msg.Text)));

                        c.Route<AgentResponse, ChatEfferent>(
                            (r, ct) => Task.FromResult(
                                new ChatEfferent("BOT", r.Text)));

                        c.Terminal<AgentThought>();
                    });
            })
            .Build();

        // Enqueue multiple responses
        host.Gemini.EnqueueResponse("First response");
        host.Gemini.EnqueueResponse("Second response");

        await host.StartAsync();

        // Act - send first message and wait for response
        await host.Console.SendInputAsync("First question");
        string firstOutput = await host.Console.WaitForOutputContainingAsync("First response", TimeSpan.FromSeconds(10));

        // Send second message and wait for response
        await host.Console.SendInputAsync("Second question");
        string secondOutput = await host.Console.WaitForOutputContainingAsync("Second response", TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("First response", firstOutput);
        Assert.Contains("Second response", secondOutput);
        Assert.Equal(2, host.Gemini.SentMessages.Count);
    }
}
