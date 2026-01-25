// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.OpenAI;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the ConsoleOpenAI toy application.
/// Validates that chat messages are routed to the OpenAI gateway and responses appear on console.
/// </summary>
public sealed class ConsoleOpenAITests
{
    /// <summary>
    /// Tests that a user message is sent to the OpenAI gateway and the scripted response appears on console.
    /// </summary>
    [Fact]
    public async Task UserMessageSentToOpenAIResponseAppearsOnConsole()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualOpenAI()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                OpenAIClientConfig openAiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gpt-4o"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig);

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
        host.OpenAI.EnqueueResponse("Hello! I am an AI assistant.");

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
    /// Tests that the user message is correctly captured by the OpenAI gateway.
    /// </summary>
    [Fact]
    public async Task UserMessageCapturedByGateway()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualOpenAI()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                OpenAIClientConfig openAiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gpt-4o"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig);

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

        host.OpenAI.EnqueueResponse("Response from AI");

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the meaning of life?");

        // Wait for response to ensure message was processed
        await host.Console.WaitForOutputContainingAsync("Response from AI", TimeSpan.FromSeconds(10));

        // Assert - verify the gateway captured the message
        Assert.Single(host.OpenAI.SentMessages);
        Assert.Contains("What is the meaning of life?", host.OpenAI.SentMessages[0].Text);
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
            .UseVirtualOpenAI()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                OpenAIClientConfig openAiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gpt-4o"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig);

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

        host.OpenAI.EnqueueResponse("AI response text");

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
            .UseVirtualOpenAI()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                OpenAIClientConfig openAiConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "gpt-4o"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig);

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
        host.OpenAI.EnqueueResponse("First response");
        host.OpenAI.EnqueueResponse("Second response");

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
        Assert.Equal(2, host.OpenAI.SentMessages.Count);
    }
}
