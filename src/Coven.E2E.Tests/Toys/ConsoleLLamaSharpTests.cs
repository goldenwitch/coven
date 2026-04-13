// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.LLamaSharp;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the ConsoleLLamaSharp toy application.
/// Validates that chat messages are routed to the LLamaSharp gateway and responses appear on console.
/// </summary>
public sealed class ConsoleLLamaSharpTests
{
    /// <summary>
    /// Tests that a user message is sent to the LLamaSharp gateway and the scripted response appears on console.
    /// </summary>
    [Fact]
    public async Task UserMessageSentToLLamaSharpResponseAppearsOnConsole()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualLLamaSharp()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                LLamaSharpClientConfig llamaConfig = new()
                {
                    ModelPath = "test-model.gguf"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig);

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
                    });
            })
            .Build();

        // Enqueue scripted response before starting
        host.LLamaSharp.EnqueueResponse("Hello! I am a local AI assistant.");

        await host.StartAsync();

        // Act - send user message
        await host.Console.SendInputAsync("Hello, AI!");

        // Assert - verify response appears on console
        string output = await host.Console.WaitForOutputContainingAsync(
            "I am a local AI assistant",
            TimeSpan.FromSeconds(10));

        Assert.Contains("I am a local AI assistant", output);
    }

    /// <summary>
    /// Tests that the user message is correctly captured by the LLamaSharp gateway.
    /// </summary>
    [Fact]
    public async Task UserMessageCapturedByGateway()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualLLamaSharp()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                LLamaSharpClientConfig llamaConfig = new()
                {
                    ModelPath = "test-model.gguf"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig);

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
                    });
            })
            .Build();

        host.LLamaSharp.EnqueueResponse("Test response");

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the meaning of life?");
        await host.Console.WaitForOutputContainingAsync("Test response", TimeSpan.FromSeconds(10));

        // Assert
        IReadOnlyList<LLamaSharpEfferent> sent = host.LLamaSharp.SentMessages;
        Assert.Single(sent);
        Assert.Contains("What is the meaning of life?", sent[0].Text);
    }

    /// <summary>
    /// Tests that the conversation is recorded correctly in the journals.
    /// </summary>
    [Fact]
    public async Task ConversationRecordedInJournal()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualLLamaSharp()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                LLamaSharpClientConfig llamaConfig = new()
                {
                    ModelPath = "test-model.gguf"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig);

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
                    });
            })
            .Build();

        host.LLamaSharp.EnqueueResponse("42 is the answer.");

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is 6 times 7?");
        await host.Console.WaitForOutputContainingAsync("42 is the answer", TimeSpan.FromSeconds(10));

        // Assert - check agent journal has both prompt and response
        IReadOnlyList<AgentPrompt> prompts = await host.Journals.GetEntriesAsync<AgentEntry, AgentPrompt>();
        IReadOnlyList<AgentResponse> responses = await host.Journals.GetEntriesAsync<AgentEntry, AgentResponse>();

        Assert.Single(prompts);
        Assert.Contains("What is 6 times 7?", prompts[0].Text);
        Assert.Single(responses);
        Assert.Contains("42 is the answer", responses[0].Text);
    }

    /// <summary>
    /// Tests that multiple exchanges are processed correctly in sequence.
    /// </summary>
    [Fact]
    public async Task MultipleExchangesProcessedCorrectly()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualLLamaSharp()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                LLamaSharpClientConfig llamaConfig = new()
                {
                    ModelPath = "test-model.gguf"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig);

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
                    });
            })
            .Build();

        host.LLamaSharp.EnqueueResponse("First response from local model.");
        host.LLamaSharp.EnqueueResponse("Second response from local model.");

        await host.StartAsync();

        // Act - first exchange
        await host.Console.SendInputAsync("First question");
        await host.Console.WaitForOutputContainingAsync("First response", TimeSpan.FromSeconds(10));

        // Act - second exchange
        await host.Console.SendInputAsync("Second question");
        await host.Console.WaitForOutputContainingAsync("Second response", TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(2, host.LLamaSharp.SentMessages.Count);
        Assert.Equal(0, host.LLamaSharp.PendingResponseCount);
    }
}
