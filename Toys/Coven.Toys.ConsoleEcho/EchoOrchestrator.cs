using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Chat;
using Coven.Chat.Adapter;

namespace Coven.Toys.ConsoleEcho;

// Orchestrator that starts the console adapter host and echoes user input back
// as ChatResponse entries written to the shared scrivener.
internal sealed class EchoOrchestrator : BackgroundService
{
    private readonly IAdapterHost<ChatEntry> _adapterHost;
    private readonly IAdapter<ChatEntry> _adapter;
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly ILogger<EchoOrchestrator> _logger;

    public EchoOrchestrator(
        IAdapterHost<ChatEntry> adapterHost,
        IAdapter<ChatEntry> adapter,
        IScrivener<ChatEntry> scrivener,
        ILogger<EchoOrchestrator> logger)
    {
        _adapterHost = adapterHost;
        _adapter = adapter;
        _scrivener = scrivener;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConsoleEcho started. Type to echo. Ctrl+C to exit.");

        // Start the adapter host which pumps console <-> scrivener
        Task pump = _adapterHost.RunAsync(_scrivener, _adapter, stoppingToken);

        // Echo loop: mirror ChatThought as ChatResponse
        Task echo = Task.Run(async () =>
        {
            long after = 0;
            await foreach ((long journalPosition, ChatEntry entry) in _scrivener.TailAsync(after, stoppingToken).ConfigureAwait(false))
            {
                after = journalPosition;
                if (entry is ChatThought t)
                {
                    ChatResponse response = new("echo", t.Text);
                    try
                    {
                        _ = await _scrivener.WriteAsync(response, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, stoppingToken);

        try
        {
            await Task.WhenAll(pump, echo).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }
}
