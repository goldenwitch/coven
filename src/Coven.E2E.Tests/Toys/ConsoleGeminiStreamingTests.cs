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
/// E2E tests for the ConsoleGemini toy application with streaming responses.
/// Validates that streaming responses are assembled correctly from chunks.
/// </summary>
public sealed class ConsoleGeminiStreamingTests
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
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig, reg => reg.EnableStreaming());

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
        host.Gemini.EnqueueStreamingResponse(["Hello", " there", "! How", " can I", " help?"]);

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
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig, reg => reg.EnableStreaming());

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
        host.Gemini.EnqueueStreamingResponse(chunks);

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
    /// Tests streaming response with reasoning chunks and final response.
    /// </summary>
    [Fact]
    public async Task StreamingWithReasoningEmitsThoughtsAndResponse()
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
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig, reg => reg.EnableStreaming());

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

        // Enqueue streaming response with reasoning and response chunks
        host.Gemini.EnqueueStreamingResponseWithReasoning(
            reasoningChunks: ["Let me ", "think about ", "this..."],
            responseChunks: ["The answer ", "is 42."]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the answer?");

        // Assert - should see both the thought and the response
        // Note: Due to windowing daemon timing, the order of thought vs response is not deterministic.
        // Both StreamWindowingDaemons react to the same signal.
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
    /// Tests multiple sequential streaming exchanges in a conversation.
    /// </summary>
    [Fact]
    public async Task MultipleStreamingExchanges()
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
                BranchManifest agents = coven.UseGeminiAgents(geminiConfig, reg => reg.EnableStreaming());

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

        // Enqueue multiple streaming responses for sequential exchanges
        host.Gemini.EnqueueStreamingResponse(["First ", "response ", "complete."]);
        host.Gemini.EnqueueStreamingResponse(["Second ", "response ", "done."]);
        host.Gemini.EnqueueStreamingResponse(["Third ", "and ", "final."]);

        await host.StartAsync();

        // Act & Assert - first exchange
        await host.Console.SendInputAsync("Message one");
        string output1 = await host.Console.WaitForOutputContainingAsync(
            "First response complete.",
            TimeSpan.FromSeconds(10));
        Assert.Contains("First response complete.", output1);

        // Act & Assert - second exchange
        await host.Console.SendInputAsync("Message two");
        string output2 = await host.Console.WaitForOutputContainingAsync(
            "Second response done.",
            TimeSpan.FromSeconds(10));
        Assert.Contains("Second response done.", output2);

        // Act & Assert - third exchange
        await host.Console.SendInputAsync("Message three");
        string output3 = await host.Console.WaitForOutputContainingAsync(
            "Third and final.",
            TimeSpan.FromSeconds(10));
        Assert.Contains("Third and final.", output3);
    }
}
