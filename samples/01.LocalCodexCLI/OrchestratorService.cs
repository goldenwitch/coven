using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Core;
using Coven.Chat;
using Coven.Chat.Adapter;

namespace Coven.Samples.LocalCodexCLI;

// This hosted service demonstrates the intended pattern:
// - Start the IAdapterHost to pump chat input/output via IScrivener<ChatEntry>.
// - Await ICoven.Ritual<...>(); the ritual advances by consuming entries provided by the adapter host.
// In a real agent pipeline, the ritual typically runs until the user or agent exits.
internal sealed class SampleOrchestrator : BackgroundService
{
    private readonly ICoven _coven;
    private readonly IAdapterHost<ChatEntry> _adapterHost;
    private readonly IAdapter<ChatEntry> _adapter;
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly ILogger<SampleOrchestrator> _logger;

    public SampleOrchestrator(
        ICoven coven,
        IAdapterHost<ChatEntry> adapterHost,
        IAdapter<ChatEntry> adapter,
        IScrivener<ChatEntry> scrivener,
        ILogger<SampleOrchestrator> logger)
    {
        _coven = coven;
        _adapterHost = adapterHost;
        _adapter = adapter;
        _scrivener = scrivener;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start console adapter pump (runs until stoppingToken is cancelled)
        var adapterTask = _adapterHost.RunAsync(_scrivener, _adapter, stoppingToken);

        // Start the ritual. In a real agent pipeline this will remain active
        // and progress using messages pumped by the adapter host.
        try
        {
            _logger.LogInformation("Starting ritual; chat I/O via ConsoleAdapter.");
            var output = await _coven.Ritual<string>();
            _logger.LogInformation("Ritual completed with output: {Output}", output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ritual failed");
        }

        // Keep service alive until cancellation; observe adapter task
        try
        {
            await Task.WhenAny(adapterTask, Task.Delay(Timeout.Infinite, stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }
}
