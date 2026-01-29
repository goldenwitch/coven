// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.Claude;
using Coven.Core;
using Coven.Testing.Harness.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Testing.Harness;

/// <summary>
/// Virtual Claude gateway implementation for E2E testing.
/// Allows tests to script responses and inspect sent messages.
/// </summary>
/// <remarks>
/// This implements the internal <see cref="IClaudeGatewayConnection"/> interface
/// from Coven.Agents.Claude via InternalsVisibleTo.
/// The gateway stores a reference to the daemon scope's service provider,
/// allowing it to resolve the correct scoped scrivener instance when emitting responses.
/// </remarks>
public sealed class VirtualClaudeGateway : IClaudeGatewayConnection
{
    private readonly Queue<IScriptedClaudeResponse> _responses = new();
    private readonly List<ClaudeEfferent> _sentMessages = [];
    private readonly Lock _lock = new();

    private IServiceProvider? _scopedProvider;

    /// <summary>
    /// Sets the daemon scope's service provider for scrivener resolution.
    /// This must be called after the daemon scope is created during E2E test startup.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider, or null to clear.</param>
    public void SetScopedProvider(IServiceProvider? serviceProvider)
    {
        _scopedProvider = serviceProvider;
    }

    private IScrivener<ClaudeEntry> GetScrivener()
    {
        IServiceProvider provider = _scopedProvider
            ?? throw new InvalidOperationException(
                "VirtualClaudeGateway cannot resolve scrivener: no active scope. " +
                "Ensure SetScopedProvider is called when entering the daemon scope.");

        return provider.GetRequiredKeyedService<IScrivener<ClaudeEntry>>("Coven.InternalClaudeScrivener");
    }

    // === Test Setup API ===

    /// <summary>
    /// Enqueues a complete (non-streaming) response.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="model">Optional model name (defaults to claude-sonnet-4-20250514).</param>
    public void EnqueueResponse(string content, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedClaudeCompleteResponse(content, model ?? "claude-sonnet-4-20250514"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response as a sequence of chunks.
    /// </summary>
    /// <param name="chunks">The response chunks.</param>
    /// <param name="model">Optional model name (defaults to claude-sonnet-4-20250514).</param>
    public void EnqueueStreamingResponse(IEnumerable<string> chunks, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedClaudeStreamingResponse([.. chunks], model ?? "claude-sonnet-4-20250514"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response with thinking chunks followed by response chunks.
    /// </summary>
    /// <param name="thinkingChunks">The thinking/reasoning chunks.</param>
    /// <param name="responseChunks">The response content chunks.</param>
    /// <param name="model">Optional model name (defaults to claude-sonnet-4-20250514).</param>
    public void EnqueueStreamingResponseWithThinking(
        IEnumerable<string> thinkingChunks,
        IEnumerable<string> responseChunks,
        string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedClaudeStreamingWithThinkingResponse(
                [.. thinkingChunks],
                [.. responseChunks],
                model ?? "claude-sonnet-4-20250514"));
        }
    }

    /// <summary>
    /// Enqueues a pre-built scripted response.
    /// </summary>
    /// <param name="response">The scripted response to enqueue.</param>
    public void EnqueueResponse(IScriptedClaudeResponse response)
    {
        lock (_lock)
        {
            _responses.Enqueue(response);
        }
    }

    // === Test Output API ===

    /// <summary>
    /// Gets all messages that have been sent to Claude through this gateway.
    /// </summary>
    public IReadOnlyList<ClaudeEfferent> SentMessages
    {
        get
        {
            lock (_lock)
            {
                return [.. _sentMessages];
            }
        }
    }

    /// <summary>
    /// Clears the sent messages list.
    /// </summary>
    public void ClearSentMessages()
    {
        lock (_lock)
        {
            _sentMessages.Clear();
        }
    }

    /// <summary>
    /// Gets the number of scripted responses remaining in the queue.
    /// </summary>
    public int PendingResponseCount
    {
        get
        {
            lock (_lock)
            {
                return _responses.Count;
            }
        }
    }

    // === IClaudeGatewayConnection Implementation ===

    /// <inheritdoc />
    public Task ConnectAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(ClaudeEfferent outgoing, CancellationToken cancellationToken)
    {
        IScriptedClaudeResponse response;
        lock (_lock)
        {
            _sentMessages.Add(outgoing);

            if (!_responses.TryDequeue(out response!))
            {
                string preview = outgoing.Text.Length > 50
                    ? outgoing.Text[..50] + "..."
                    : outgoing.Text;
                throw new InvalidOperationException(
                    $"No scripted response available for message: {preview}");
            }
        }

        string messageId = Guid.NewGuid().ToString("N");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        await EmitResponseAsync(response, messageId, timestamp, cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitResponseAsync(
        IScriptedClaudeResponse response,
        string messageId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        switch (response)
        {
            case ScriptedClaudeCompleteResponse complete:
                await EmitCompleteResponseAsync(complete, messageId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedClaudeStreamingResponse streaming:
                await EmitStreamingResponseAsync(streaming, messageId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedClaudeStreamingWithThinkingResponse withThinking:
                await EmitStreamingWithThinkingResponseAsync(withThinking, messageId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unknown scripted response type: {response.GetType().Name}");
        }
    }

    private async Task EmitCompleteResponseAsync(
        ScriptedClaudeCompleteResponse response,
        string messageId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<ClaudeEntry> scrivener = GetScrivener();

        // For complete (non-streaming) responses, emit ClaudeAfferent (complete response)
        // rather than ClaudeAfferentChunk + ClaudeStreamCompleted which requires windowing daemons.
        await scrivener.WriteAsync(new ClaudeAfferent(
            Sender: "claude",
            Text: response.Content,
            MessageId: messageId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingResponseAsync(
        ScriptedClaudeStreamingResponse response,
        string messageId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<ClaudeEntry> scrivener = GetScrivener();

        foreach (string chunk in response.Chunks)
        {
            await scrivener.WriteAsync(new ClaudeAfferentChunk(
                Sender: "claude",
                Text: chunk,
                MessageId: messageId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new ClaudeStreamCompleted(
            Sender: "claude",
            MessageId: messageId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingWithThinkingResponseAsync(
        ScriptedClaudeStreamingWithThinkingResponse response,
        string messageId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<ClaudeEntry> scrivener = GetScrivener();

        // Emit thinking chunks first
        foreach (string thinking in response.ThinkingChunks)
        {
            await scrivener.WriteAsync(new ClaudeAfferentThinkingChunk(
                Sender: "claude",
                Text: thinking,
                MessageId: messageId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        // Then emit response chunks
        foreach (string chunk in response.ResponseChunks)
        {
            await scrivener.WriteAsync(new ClaudeAfferentChunk(
                Sender: "claude",
                Text: chunk,
                MessageId: messageId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new ClaudeStreamCompleted(
            Sender: "claude",
            MessageId: messageId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }
}
