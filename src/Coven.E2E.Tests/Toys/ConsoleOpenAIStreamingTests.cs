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
/// E2E tests for the ConsoleOpenAIStreaming toy application.
/// Validates that streaming responses are assembled correctly from chunks.
/// </summary>
public sealed class ConsoleOpenAIStreamingTests
{
    /// <summary>
    /// Tests that streaming chunks appear progressively and are assembled into a complete response.
    /// </summary>
    [Fact]
    public async Task StreamingChunksAppearProgressively()
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
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

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

                        // Thoughts displayed to console
                        c.Route<AgentThought, ChatEfferent>(
                            (t, ct) => Task.FromResult(
                                new ChatEfferent("BOT", t.Text)));

                        // Streaming: chunks are terminal (console doesn't support chunk display)
                        c.Terminal<AgentAfferentChunk>();
                        c.Terminal<AgentAfferentThoughtChunk>();
                    });
            })
            .Build();

        // Enqueue streaming response with multiple chunks
        host.OpenAI.EnqueueStreamingResponse(["Hello", " there", "! How", " can I", " help?"]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Hi there!");

        // Assert - the final assembled response should appear
        string output = await host.Console.WaitForOutputContainingAsync(
            "Hello there! How can I help?",
            TimeSpan.FromSeconds(10));

        Assert.Contains("Hello there! How can I help?", output);
    }

    /// <summary>
    /// Tests that the complete message is assembled from streaming chunks.
    /// </summary>
    [Fact]
    public async Task CompleteMessageAssembledFromChunks()
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
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

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
                        c.Terminal<AgentAfferentChunk>();
                        c.Terminal<AgentAfferentThoughtChunk>();
                    });
            })
            .Build();

        // Enqueue response with many small chunks that form a complete sentence
        string[] chunks = ["The ", "quick ", "brown ", "fox ", "jumps ", "over ", "the ", "lazy ", "dog."];
        host.OpenAI.EnqueueStreamingResponse(chunks);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Test sentence");

        // Assert - complete assembled message
        string output = await host.Console.WaitForOutputContainingAsync(
            "The quick brown fox jumps over the lazy dog.",
            TimeSpan.FromSeconds(10));

        Assert.Contains("The quick brown fox jumps over the lazy dog.", output);
    }

    /// <summary>
    /// Tests streaming response with thoughts (reasoning tokens).
    /// </summary>
    [Fact]
    public async Task StreamingWithThoughtsProcessedCorrectly()
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
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

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

                        // Route thoughts to chat for visibility in this test
                        c.Route<AgentThought, ChatEfferent>(
                            (t, ct) => Task.FromResult(
                                new ChatEfferent("BOT", "[Thinking] " + t.Text)));

                        c.Terminal<AgentAfferentChunk>();
                        c.Terminal<AgentAfferentThoughtChunk>();
                    });
            })
            .Build();

        // Enqueue streaming response with thoughts and response chunks
        host.OpenAI.EnqueueStreamingResponseWithThoughts(
            thoughtChunks: ["Let me ", "think about ", "this..."],
            responseChunks: ["The answer ", "is 42."]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the answer?");

        // Assert - should see both the thought and the response
        // Note: Due to windowing daemon timing, the order of thought vs response is not deterministic.
        // Both StreamWindowingDaemons react to the same AgentStreamCompleted signal.
        IReadOnlyList<string> outputs = await host.Console.CollectOutputAsync(2, TimeSpan.FromSeconds(10));

        // One output should be the thought (with [Thinking] prefix and assembled text)
        string? thoughtOutput = outputs.FirstOrDefault(o => o.Contains("[Thinking]"));
        Assert.NotNull(thoughtOutput);
        Assert.Contains("think about this", thoughtOutput);

        // One output should be the response
        string? responseOutput = outputs.FirstOrDefault(o => o.Contains("The answer is 42"));
        Assert.NotNull(responseOutput);
    }

    /// <summary>
    /// Tests that agent journal correctly records chunks and final response.
    /// </summary>
    [Fact]
    public async Task StreamingJournalRecordsChunksAndResponse()
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
                BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

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
                        c.Terminal<AgentAfferentChunk>();
                        c.Terminal<AgentAfferentThoughtChunk>();
                    });
            })
            .Build();

        host.OpenAI.EnqueueStreamingResponse(["Chunk1", "Chunk2", "Chunk3"]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Streaming test");
        await host.Console.WaitForOutputContainingAsync("Chunk", TimeSpan.FromSeconds(10));

        // Assert - journal should have afferent chunks
        IReadOnlyList<AgentEntry> entries = await host.Journals.GetEntriesAsync<AgentEntry>();

        // Should have prompt
        Assert.Contains(entries, e => e is AgentPrompt { Text: "Streaming test" });

        // Should have afferent chunks (received from gateway)
        IReadOnlyList<AgentAfferentChunk> chunks = await host.Journals.GetEntriesAsync<AgentEntry, AgentAfferentChunk>();
        Assert.True(chunks.Count >= 3, $"Expected at least 3 chunks, got {chunks.Count}");

        // Should have final assembled response
        Assert.Contains(entries, e => e is AgentResponse);
    }
}
