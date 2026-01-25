// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.OpenAI;
using Coven.Core;
using Coven.Testing.Harness.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Testing.Harness;

/// <summary>
/// Virtual OpenAI gateway implementation for E2E testing.
/// Allows tests to script responses and inspect sent messages.
/// </summary>
/// <remarks>
/// This implements the internal <see cref="IOpenAIGatewayConnection"/> interface
/// from Coven.Agents.OpenAI via InternalsVisibleTo.
/// The gateway stores a reference to the daemon scope's service provider,
/// allowing it to resolve the correct scoped scrivener instance when emitting responses.
/// </remarks>
public sealed class VirtualOpenAIGateway : IOpenAIGatewayConnection
{
    private readonly Queue<IScriptedResponse> _responses = new();
    private readonly List<OpenAIEfferent> _sentMessages = [];
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

    private IScrivener<OpenAIEntry> GetScrivener()
    {
        IServiceProvider provider = _scopedProvider
            ?? throw new InvalidOperationException(
                "VirtualOpenAIGateway cannot resolve scrivener: no active scope. " +
                "Ensure SetScopedProvider is called when entering the daemon scope.");

        return provider.GetRequiredKeyedService<IScrivener<OpenAIEntry>>("Coven.InternalOpenAIScrivener");
    }

    // === Test Setup API ===

    /// <summary>
    /// Enqueues a complete (non-streaming) response.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="model">Optional model name (defaults to gpt-4o).</param>
    public void EnqueueResponse(string content, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedCompleteResponse(content, model ?? "gpt-4o"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response as a sequence of chunks.
    /// </summary>
    /// <param name="chunks">The response chunks.</param>
    /// <param name="model">Optional model name (defaults to gpt-4o).</param>
    public void EnqueueStreamingResponse(IEnumerable<string> chunks, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedStreamingResponse([.. chunks], model ?? "gpt-4o"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response with thought/reasoning chunks followed by response chunks.
    /// </summary>
    /// <param name="thoughtChunks">The thought/reasoning chunks.</param>
    /// <param name="responseChunks">The response content chunks.</param>
    /// <param name="model">Optional model name (defaults to gpt-4o).</param>
    public void EnqueueStreamingResponseWithThoughts(
        IEnumerable<string> thoughtChunks,
        IEnumerable<string> responseChunks,
        string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedStreamingWithThoughtsResponse(
                [.. thoughtChunks],
                [.. responseChunks],
                model ?? "gpt-4o"));
        }
    }

    /// <summary>
    /// Enqueues a pre-built scripted response.
    /// </summary>
    /// <param name="response">The scripted response to enqueue.</param>
    public void EnqueueResponse(IScriptedResponse response)
    {
        lock (_lock)
        {
            _responses.Enqueue(response);
        }
    }

    // === Test Output API ===

    /// <summary>
    /// Gets all messages that have been sent to OpenAI through this gateway.
    /// </summary>
    public IReadOnlyList<OpenAIEfferent> SentMessages
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

    // === IOpenAIGatewayConnection Implementation ===

    /// <inheritdoc />
    public Task ConnectAsync()
    {
        Console.WriteLine("[VirtualOpenAIGateway] ConnectAsync called");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(OpenAIEfferent outgoing, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[VirtualOpenAIGateway] SendAsync called with: {outgoing.Text}");
        IScriptedResponse response;
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

        Console.WriteLine("[VirtualOpenAIGateway] Emitting response");
        await EmitResponseAsync(response, responseId, timestamp, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("[VirtualOpenAIGateway] Response emitted");
    }

    private async Task EmitResponseAsync(
        IScriptedResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        switch (response)
        {
            case ScriptedCompleteResponse complete:
                await EmitCompleteResponseAsync(complete, responseId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedStreamingResponse streaming:
                await EmitStreamingResponseAsync(streaming, responseId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedStreamingWithThoughtsResponse withThoughts:
                await EmitStreamingWithThoughtsResponseAsync(withThoughts, responseId, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unknown scripted response type: {response.GetType().Name}");
        }
    }

    private async Task EmitCompleteResponseAsync(
        ScriptedCompleteResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<OpenAIEntry> scrivener = GetScrivener();

        // For complete (non-streaming) responses, emit OpenAIAfferent (complete response)
        // rather than OpenAIAfferentChunk + OpenAIStreamCompleted which requires windowing daemons.
        await scrivener.WriteAsync(new OpenAIAfferent(
            Sender: "openai",
            Text: response.Content,
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingResponseAsync(
        ScriptedStreamingResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<OpenAIEntry> scrivener = GetScrivener();

        foreach (string chunk in response.Chunks)
        {
            await scrivener.WriteAsync(new OpenAIAfferentChunk(
                Sender: "openai",
                Text: chunk,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new OpenAIStreamCompleted(
            Sender: "openai",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingWithThoughtsResponseAsync(
        ScriptedStreamingWithThoughtsResponse response,
        string responseId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<OpenAIEntry> scrivener = GetScrivener();

        // Emit thought chunks first (reasoning summary)
        foreach (string thought in response.ThoughtChunks)
        {
            await scrivener.WriteAsync(new OpenAIAfferentThoughtChunk(
                Sender: "openai",
                Text: thought,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        // Then emit response chunks
        foreach (string chunk in response.ResponseChunks)
        {
            await scrivener.WriteAsync(new OpenAIAfferentChunk(
                Sender: "openai",
                Text: chunk,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new OpenAIStreamCompleted(
            Sender: "openai",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }
}
