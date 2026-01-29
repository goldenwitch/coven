// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the ConsoleClaude toy application with streaming responses.
/// Validates that streaming responses are assembled correctly from chunks.
/// </summary>
public sealed class ConsoleClaudeStreamingTests
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
            .UseVirtualClaude()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                ClaudeClientConfig claudeConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "claude-sonnet-4-20250514"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseClaudeAgents(claudeConfig, reg => reg.EnableStreaming());

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
        host.Claude.EnqueueStreamingResponse(["Hello", " there", "! How", " can I", " help?"]);

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
            .UseVirtualClaude()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                ClaudeClientConfig claudeConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "claude-sonnet-4-20250514"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseClaudeAgents(claudeConfig, reg => reg.EnableStreaming());

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
        host.Claude.EnqueueStreamingResponse(chunks);

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
    /// Tests streaming response with thinking chunks and final response.
    /// </summary>
    [Fact]
    public async Task StreamingWithThinkingEmitsThoughtsAndResponse()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualClaude()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                ClaudeClientConfig claudeConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "claude-sonnet-4-20250514"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseClaudeAgents(claudeConfig, reg => reg.EnableStreaming());

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

        // Enqueue response with thinking and response chunks
        host.Claude.EnqueueStreamingResponseWithThinking(
            thinkingChunks: ["Let me ", "think about ", "this..."],
            responseChunks: ["The answer ", "is 42."]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("What is the answer?");

        // Assert - both thought and response should appear
        string output = await host.Console.WaitForOutputContainingAsync(
            "The answer is 42.",
            TimeSpan.FromSeconds(10));

        Assert.Contains("The answer is 42.", output);
    }

    /// <summary>
    /// Tests that the Claude journal records streaming entries correctly.
    /// </summary>
    [Fact]
    public async Task StreamingEntriesRecordedInJournal()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualClaude()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                ClaudeClientConfig claudeConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "claude-sonnet-4-20250514"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseClaudeAgents(claudeConfig, reg => reg.EnableStreaming());

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

        host.Claude.EnqueueStreamingResponse(["Part ", "one ", "two."]);

        await host.StartAsync();

        // Act
        await host.Console.SendInputAsync("Test streaming");
        await host.Console.WaitForOutputContainingAsync("Part one two.", TimeSpan.FromSeconds(10));

        // Assert - verify Claude journal has streaming entries
        IReadOnlyList<ClaudeEntry> claudeEntries = await host.Journals.GetEntriesAsync<ClaudeEntry>();

        // Should have: ClaudeEfferent (outgoing), multiple ClaudeAfferentChunk, ClaudeStreamCompleted
        Assert.Contains(claudeEntries, e => e is ClaudeEfferent);
        Assert.Contains(claudeEntries, e => e is ClaudeAfferentChunk);
        Assert.Contains(claudeEntries, e => e is ClaudeStreamCompleted);
    }

    /// <summary>
    /// Tests multiple streaming exchanges in sequence.
    /// </summary>
    [Fact]
    public async Task MultipleStreamingExchangesProcessedCorrectly()
    {
        // Arrange
        await using E2ETestHost host = new E2ETestHostBuilder()
            .UseVirtualConsole()
            .UseVirtualClaude()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig consoleConfig = new()
                {
                    InputSender = "console",
                    OutputSender = "BOT"
                };

                ClaudeClientConfig claudeConfig = new()
                {
                    ApiKey = "test-key",
                    Model = "claude-sonnet-4-20250514"
                };

                BranchManifest chat = coven.UseConsoleChat(consoleConfig);
                BranchManifest agents = coven.UseClaudeAgents(claudeConfig, reg => reg.EnableStreaming());

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

        // Enqueue multiple streaming responses
        host.Claude.EnqueueStreamingResponse(["First ", "streaming ", "response."]);
        host.Claude.EnqueueStreamingResponse(["Second ", "streaming ", "response."]);

        await host.StartAsync();

        // Act - first exchange
        await host.Console.SendInputAsync("First question");
        string firstOutput = await host.Console.WaitForOutputContainingAsync(
            "First streaming response.",
            TimeSpan.FromSeconds(10));

        // Second exchange
        await host.Console.SendInputAsync("Second question");
        string secondOutput = await host.Console.WaitForOutputContainingAsync(
            "Second streaming response.",
            TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("First streaming response.", firstOutput);
        Assert.Contains("Second streaming response.", secondOutput);
        Assert.Equal(2, host.Claude.SentMessages.Count);
    }
}
