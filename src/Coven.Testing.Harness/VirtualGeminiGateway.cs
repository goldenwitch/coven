// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.Gemini;
using Coven.Core;
using Coven.Testing.Harness.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Testing.Harness;

/// <summary>
/// Virtual Gemini gateway implementation for E2E testing.
/// Allows tests to script responses and inspect sent messages.
/// </summary>
/// <remarks>
/// This implements the internal <see cref="IGeminiGatewayConnection"/> interface
/// from Coven.Agents.Gemini via InternalsVisibleTo.
/// The gateway stores a reference to the daemon scope's service provider,
/// allowing it to resolve the correct scoped scrivener instance when emitting responses.
/// </remarks>
public sealed class VirtualGeminiGateway : IGeminiGatewayConnection
{
    private readonly Queue<IScriptedGeminiResponse> _responses = new();
    private readonly List<GeminiEfferent> _sentMessages = [];
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

    private IScrivener<GeminiEntry> GetScrivener()
    {
        IServiceProvider provider = _scopedProvider
            ?? throw new InvalidOperationException(
                "VirtualGeminiGateway cannot resolve scrivener: no active scope. " +
                "Ensure SetScopedProvider is called when entering the daemon scope.");

        return provider.GetRequiredKeyedService<IScrivener<GeminiEntry>>("Coven.InternalGeminiScrivener");
    }

    // === Test Setup API ===

    /// <summary>
    /// Enqueues a complete (non-streaming) response.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="model">Optional model name (defaults to gemini-2.0-flash).</param>
    public void EnqueueResponse(string content, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedGeminiCompleteResponse(content, model ?? "gemini-2.0-flash"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response as a sequence of chunks.
    /// </summary>
    /// <param name="chunks">The response chunks.</param>
    /// <param name="model">Optional model name (defaults to gemini-2.0-flash).</param>
    public void EnqueueStreamingResponse(IEnumerable<string> chunks, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedGeminiStreamingResponse([.. chunks], model ?? "gemini-2.0-flash"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response with reasoning chunks followed by response chunks.
    /// </summary>
    /// <param name="reasoningChunks">The reasoning/trace chunks.</param>
    /// <param name="responseChunks">The response content chunks.</param>
    /// <param name="model">Optional model name (defaults to gemini-2.0-flash).</param>
    public void EnqueueStreamingResponseWithReasoning(
        IEnumerable<string> reasoningChunks,
        IEnumerable<string> responseChunks,
        string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedGeminiStreamingWithReasoningResponse(
                [.. reasoningChunks],
                [.. responseChunks],
                model ?? "gemini-2.0-flash"));
        }
    }

    /// <summary>
    /// Enqueues a pre-built scripted response.
    /// </summary>
    /// <param name="response">The scripted response to enqueue.</param>
    public void EnqueueResponse(IScriptedGeminiResponse response)
    {
        lock (_lock)
        {
            _responses.Enqueue(response);
        }
    }

    // === Test Output API ===

    /// <summary>
    /// Gets all messages that have been sent to Gemini through this gateway.
    /// </summary>
    public IReadOnlyList<GeminiEfferent> SentMessages
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

    // === IGeminiGatewayConnection Implementation ===

    /// <inheritdoc />
    public Task ConnectAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(GeminiEfferent outgoing, CancellationToken cancellationToken)
    {
        IScriptedGeminiResponse response;
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

        string responseId = Guid.NewGuid().ToString("N");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        await EmitResponseAsync(response, responseId, timestamp, cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitResponseAsync(
        IScriptedGeminiResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        switch (response)
        {
            case ScriptedGeminiCompleteResponse complete:
                await EmitCompleteResponseAsync(complete, responseId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedGeminiStreamingResponse streaming:
                await EmitStreamingResponseAsync(streaming, responseId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedGeminiStreamingWithReasoningResponse withReasoning:
                await EmitStreamingWithReasoningResponseAsync(withReasoning, responseId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unknown scripted response type: {response.GetType().Name}");
        }
    }

    private async Task EmitCompleteResponseAsync(
        ScriptedGeminiCompleteResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<GeminiEntry> scrivener = GetScrivener();

        // For complete (non-streaming) responses, emit GeminiAfferent (complete response)
        // rather than GeminiAfferentChunk + GeminiStreamCompleted which requires windowing daemons.
        await scrivener.WriteAsync(new GeminiAfferent(
            Sender: "gemini",
            Text: response.Content,
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingResponseAsync(
        ScriptedGeminiStreamingResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<GeminiEntry> scrivener = GetScrivener();

        foreach (string chunk in response.Chunks)
        {
            await scrivener.WriteAsync(new GeminiAfferentChunk(
                Sender: "gemini",
                Text: chunk,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new GeminiStreamCompleted(
            Sender: "gemini",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingWithReasoningResponseAsync(
        ScriptedGeminiStreamingWithReasoningResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<GeminiEntry> scrivener = GetScrivener();

        // Emit reasoning chunks first
        foreach (string reasoning in response.ReasoningChunks)
        {
            await scrivener.WriteAsync(new GeminiAfferentReasoningChunk(
                Sender: "gemini",
                Text: reasoning,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        // Then emit response chunks
        foreach (string chunk in response.ResponseChunks)
        {
            await scrivener.WriteAsync(new GeminiAfferentChunk(
                Sender: "gemini",
                Text: chunk,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new GeminiStreamCompleted(
            Sender: "gemini",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }
}
