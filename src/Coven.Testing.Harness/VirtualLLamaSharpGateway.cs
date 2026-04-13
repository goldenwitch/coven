// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.LLamaSharp;
using Coven.Core;
using Coven.Testing.Harness.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Testing.Harness;

/// <summary>
/// Virtual LLamaSharp gateway implementation for E2E testing.
/// Allows tests to script responses and inspect sent messages.
/// </summary>
/// <remarks>
/// This implements the internal <see cref="ILLamaSharpGatewayConnection"/> interface
/// from Coven.Agents.LLamaSharp via InternalsVisibleTo.
/// The gateway stores a reference to the daemon scope's service provider,
/// allowing it to resolve the correct scoped scrivener instance when emitting responses.
/// </remarks>
public sealed class VirtualLLamaSharpGateway : ILLamaSharpGatewayConnection
{
    private readonly Queue<IScriptedLLamaSharpResponse> _responses = new();
    private readonly List<LLamaSharpEfferent> _sentMessages = [];
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

    private IScrivener<LLamaSharpEntry> GetScrivener()
    {
        IServiceProvider provider = _scopedProvider
            ?? throw new InvalidOperationException(
                "VirtualLLamaSharpGateway cannot resolve scrivener: no active scope. " +
                "Ensure SetScopedProvider is called when entering the daemon scope.");

        return provider.GetRequiredKeyedService<IScrivener<LLamaSharpEntry>>("Coven.InternalLLamaSharpScrivener");
    }

    // === Test Setup API ===

    /// <summary>
    /// Enqueues a complete (non-streaming) response.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="model">Optional model name (defaults to "local-model").</param>
    public void EnqueueResponse(string content, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedLLamaSharpCompleteResponse(content, model ?? "local-model"));
        }
    }

    /// <summary>
    /// Enqueues a streaming response as a sequence of chunks.
    /// </summary>
    /// <param name="chunks">The response chunks.</param>
    /// <param name="model">Optional model name (defaults to "local-model").</param>
    public void EnqueueStreamingResponse(IEnumerable<string> chunks, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedLLamaSharpStreamingResponse([.. chunks], model ?? "local-model"));
        }
    }

    /// <summary>
    /// Enqueues a pre-built scripted response.
    /// </summary>
    /// <param name="response">The scripted response to enqueue.</param>
    public void EnqueueResponse(IScriptedLLamaSharpResponse response)
    {
        lock (_lock)
        {
            _responses.Enqueue(response);
        }
    }

    // === Test Output API ===

    /// <summary>
    /// Gets all messages that have been sent to the model through this gateway.
    /// </summary>
    public IReadOnlyList<LLamaSharpEfferent> SentMessages
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

    // === ILLamaSharpGatewayConnection Implementation ===

    /// <inheritdoc />
    public Task ConnectAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(LLamaSharpEfferent outgoing, CancellationToken cancellationToken)
    {
        IScriptedLLamaSharpResponse response;
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

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        await EmitResponseAsync(response, timestamp, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task EmitResponseAsync(
        IScriptedLLamaSharpResponse response,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        switch (response)
        {
            case ScriptedLLamaSharpCompleteResponse complete:
                await EmitCompleteResponseAsync(complete, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            case ScriptedLLamaSharpStreamingResponse streaming:
                await EmitStreamingResponseAsync(streaming, timestamp, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unknown scripted response type: {response.GetType().Name}");
        }
    }

    private async Task EmitCompleteResponseAsync(
        ScriptedLLamaSharpCompleteResponse response,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<LLamaSharpEntry> scrivener = GetScrivener();

        await scrivener.WriteAsync(new LLamaSharpAfferent(
            Sender: "llamasharp",
            Text: response.Content,
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitStreamingResponseAsync(
        ScriptedLLamaSharpStreamingResponse response,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        IScrivener<LLamaSharpEntry> scrivener = GetScrivener();

        foreach (string chunk in response.Chunks)
        {
            await scrivener.WriteAsync(new LLamaSharpAfferentChunk(
                Sender: "llamasharp",
                Text: chunk,
                Timestamp: timestamp,
                Model: response.Model), cancellationToken).ConfigureAwait(false);
        }

        await scrivener.WriteAsync(new LLamaSharpStreamCompleted(
            Sender: "llamasharp",
            Timestamp: timestamp,
            Model: response.Model), cancellationToken).ConfigureAwait(false);
    }
}
