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
/// Non-streaming gateway that loads a GGUF model in-process and runs inference,
/// collecting all tokens into a single complete response.
/// </summary>
internal sealed class LLamaSharpRequestGatewayConnection(
    LLamaSharpClientConfig configuration,
    [FromKeyedServices("Coven.InternalLLamaSharpScrivener")] IScrivener<LLamaSharpEntry> journal,
    ILogger<LLamaSharpRequestGatewayConnection> logger,
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

        StringBuilder sb = new();
        await foreach (string token in _executor.InferAsync(prompt, inferParams, cancellationToken).ConfigureAwait(false))
        {
            sb.Append(token);
        }

        string responseText = sb.ToString().TrimStart();
        responseText = LLamaSharpOutputFilter.ExtractResponse(responseText, _configuration.ResponseStartMarker);

        LLamaSharpAfferent afferent = new(
            Sender: "llamasharp",
            Text: responseText,
            Timestamp: DateTimeOffset.UtcNow,
            Model: _configuration.ResolvedModelName);
        await _journal.WriteAsync(afferent, cancellationToken).ConfigureAwait(false);

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
