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
/// E2E tests for the ConsoleLLamaSharp toy application with streaming responses.
/// Validates that streaming responses are assembled correctly from chunks.
/// </summary>
public sealed class ConsoleLLamaSharpStreamingTests
{
    /// <summary>
    /// Tests that streaming chunks are assembled into a complete response on console.
    /// </summary>
    [Fact]
    public async Task StreamingChunksAppearProgressively()
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
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig, reg => reg.EnableStreaming());

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

                        // Streaming: chunks are terminal (console doesn't support chunk display)
                        c.Terminal<AgentAfferentChunk>();
                    });
            })
            .Build();

        // Enqueue streaming response with multiple chunks
        host.LLamaSharp.EnqueueStreamingResponse(["Hello", " there", "! How", " can I", " help?"]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Hi there!");

        // Assert - verify assembled response appears
        string output = await host.Console.WaitForOutputContainingAsync(
            "Hello there! How can I help?",
            TimeSpan.FromSeconds(10));

        Assert.Contains("Hello there! How can I help?", output);
    }

    /// <summary>
    /// Tests that a complete message is correctly assembled from many small chunks.
    /// </summary>
    [Fact]
    public async Task CompleteMessageAssembledFromChunks()
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
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig, reg => reg.EnableStreaming());

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

                        c.Terminal<AgentAfferentChunk>();
                    });
            })
            .Build();

        // Many small chunks
        host.LLamaSharp.EnqueueStreamingResponse(["T", "h", "e", " ", "a", "n", "s", "w", "e", "r", " ", "i", "s", " ", "4", "2"]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the meaning of life?");

        // Assert
        string output = await host.Console.WaitForOutputContainingAsync(
            "The answer is 42",
            TimeSpan.FromSeconds(10));

        Assert.Contains("The answer is 42", output);
    }

    /// <summary>
    /// Tests that streaming entries are recorded in the journal.
    /// </summary>
    [Fact]
    public async Task StreamingEntriesRecordedInJournal()
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
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig, reg => reg.EnableStreaming());

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

                        c.Terminal<AgentAfferentChunk>();
                    });
            })
            .Build();

        host.LLamaSharp.EnqueueStreamingResponse(["Hello", " world"]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Say something");
        await host.Console.WaitForOutputContainingAsync("Hello world", TimeSpan.FromSeconds(10));

        // Assert - check LLamaSharp journal has streaming entries
        IReadOnlyList<LLamaSharpAfferentChunk> chunks =
            await host.Journals.GetEntriesAsync<LLamaSharpEntry, LLamaSharpAfferentChunk>();
        IReadOnlyList<LLamaSharpStreamCompleted> completions =
            await host.Journals.GetEntriesAsync<LLamaSharpEntry, LLamaSharpStreamCompleted>();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Hello", chunks[0].Text);
        Assert.Equal(" world", chunks[1].Text);
        Assert.Single(completions);
    }

    /// <summary>
    /// Tests that multiple streaming exchanges are processed correctly.
    /// </summary>
    [Fact]
    public async Task MultipleStreamingExchangesProcessedCorrectly()
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
                BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig, reg => reg.EnableStreaming());

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

                        c.Terminal<AgentAfferentChunk>();
                    });
            })
            .Build();

        host.LLamaSharp.EnqueueStreamingResponse(["First", " response"]);
        host.LLamaSharp.EnqueueStreamingResponse(["Second", " response"]);

        await host.StartAsync();

        // First exchange
        await host.Console.SendInputAsync("Question one");
        await host.Console.WaitForOutputContainingAsync("First response", TimeSpan.FromSeconds(10));

        // Second exchange
        await host.Console.SendInputAsync("Question two");
        await host.Console.WaitForOutputContainingAsync("Second response", TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(2, host.LLamaSharp.SentMessages.Count);
        Assert.Equal(0, host.LLamaSharp.PendingResponseCount);
    }
}
