// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using System.Text;
using Coven.Core;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Streaming gateway that loads a GGUF model in-process and writes each generated token
/// as a <see cref="LLamaSharpAfferentChunk"/>, followed by a <see cref="LLamaSharpStreamCompleted"/> marker.
/// </summary>
internal sealed class LLamaSharpStreamingGatewayConnection(
    LLamaSharpClientConfig configuration,
    [FromKeyedServices("Coven.InternalLLamaSharpScrivener")] IScrivener<LLamaSharpEntry> journal,
    ILogger<LLamaSharpStreamingGatewayConnection> logger,
    ILLamaSharpTranscriptBuilder transcriptBuilder) : ILLamaSharpGatewayConnection
{
    private readonly LLamaSharpClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<LLamaSharpEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ILLamaSharpTranscriptBuilder _transcriptBuilder = transcriptBuilder ?? throw new ArgumentNullException(nameof(transcriptBuilder));

    private LLamaWeights? _weights;
    private StatelessExecutor? _executor;
    private ModelParams? _modelParams;

    public async Task ConnectAsync()
    {
        LLamaSharpLog.ModelLoading(_logger, _configuration.ModelPath);
        Stopwatch sw = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            _modelParams = BuildModelParams();
            _weights = LLamaWeights.LoadFromFile(_modelParams);
            _executor = new StatelessExecutor(_weights, _modelParams);
        }).ConfigureAwait(false);

        sw.Stop();
        LLamaSharpLog.ModelLoaded(_logger, sw.ElapsedMilliseconds);
        LLamaSharpLog.Connected(_logger);
    }

    public async Task SendAsync(LLamaSharpEfferent outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_executor is null)
        {
            throw new InvalidOperationException("Gateway not connected. Call ConnectAsync first.");
        }

        LLamaSharpLog.OutboundSendStart(_logger);

        string prompt = await _transcriptBuilder.BuildAsync(_weights!, outgoing, _configuration.HistoryClip, cancellationToken).ConfigureAwait(false);
        InferenceParams inferParams = BuildInferenceParams();

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string model = _configuration.ResolvedModelName;
        bool firstToken = true;
        string? responseStartMarker = _configuration.ResponseStartMarker;
        bool markerFound = responseStartMarker is null;
        StringBuilder? buffer = markerFound ? null : new StringBuilder();

        await foreach (string token in _executor.InferAsync(prompt, inferParams, cancellationToken).ConfigureAwait(false))
        {
            if (!markerFound)
            {
                buffer!.Append(token);
                string accumulated = buffer!.ToString();
                int markerIndex = accumulated.IndexOf(responseStartMarker!, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    continue;
                }

                // Marker found — emit any text after it as the first token
                markerFound = true;
                string remaining = accumulated[(markerIndex + responseStartMarker!.Length)..];
                buffer = null;

                string text = remaining.TrimStart();
                if (text.Length == 0)
                {
                    continue;
                }

                firstToken = false;
                LLamaSharpLog.StreamToken(_logger, text);
                LLamaSharpAfferentChunk firstChunk = new(
                    Sender: "llamasharp",
                    Text: text,
                    Timestamp: timestamp,
                    Model: model);
                await _journal.WriteAsync(firstChunk, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Normal streaming path
            string tokenText = firstToken ? token.TrimStart() : token;
            firstToken = false;

            if (tokenText.Length == 0)
            {
                continue;
            }

            LLamaSharpLog.StreamToken(_logger, tokenText);

            LLamaSharpAfferentChunk chunk = new(
                Sender: "llamasharp",
                Text: tokenText,
                Timestamp: timestamp,
                Model: model);
            await _journal.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
        }

        LLamaSharpStreamCompleted done = new(
            Sender: "llamasharp",
            Timestamp: timestamp,
            Model: model);
        await _journal.WriteAsync(done, cancellationToken).ConfigureAwait(false);

        LLamaSharpLog.OutboundSendSucceeded(_logger);
    }

    public ValueTask DisposeAsync()
    {
        _executor = null;
        _weights?.Dispose();
        _weights = null;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private ModelParams BuildModelParams()
    {
        ModelParams modelParams = new(_configuration.ModelPath)
        {
            ContextSize = _configuration.ContextSize,
            GpuLayerCount = _configuration.GpuLayerCount
        };

        if (_configuration.Threads.HasValue)
        {
            modelParams.Threads = _configuration.Threads.Value;
        }

        return modelParams;
    }

    private InferenceParams BuildInferenceParams()
    {
        InferenceParams inferParams = new()
        {
            MaxTokens = _configuration.MaxTokens ?? 256,
            AntiPrompts = []
        };

        if (_configuration.Temperature.HasValue || _configuration.TopP.HasValue)
        {
            inferParams.SamplingPipeline = (_configuration.Temperature, _configuration.TopP) switch
            {
                (float temp, float top) => new DefaultSamplingPipeline { Temperature = temp, TopP = top },
                (float temp, _) => new DefaultSamplingPipeline { Temperature = temp },
                (_, float top) => new DefaultSamplingPipeline { TopP = top },
                _ => inferParams.SamplingPipeline
            };
        }

        return inferParams;
    }
}
