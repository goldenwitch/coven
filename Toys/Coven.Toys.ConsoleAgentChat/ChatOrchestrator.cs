using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Core;
using Coven.Chat;
using Coven.Chat.Adapter;

namespace Coven.Toys.ConsoleAgentChat;

// Orchestrates adapter I/O and kicks off the Coven ritual that starts the agent.
internal sealed class ChatOrchestrator : BackgroundService
{
    private readonly ICoven _coven;
    private readonly IAdapterHost<ChatEntry> _adapterHost;
    private readonly IAdapter<ChatEntry> _adapter;
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly ILogger<ChatOrchestrator> _logger;

    public ChatOrchestrator(
        ICoven coven,
        IAdapterHost<ChatEntry> adapterHost,
        IAdapter<ChatEntry> adapter,
        IScrivener<ChatEntry> scrivener,
        ILogger<ChatOrchestrator> logger)
    {
        _coven = coven;
        _adapterHost = adapterHost;
        _adapter = adapter;
        _scrivener = scrivener;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConsoleAgentChat started. Type to chat. Ctrl+C to exit.");

        Task adapterTask = _adapterHost.RunAsync(_scrivener, _adapter, stoppingToken);

        try
        {
            _logger.LogInformation("Starting ritual to run agent.");
            _ = await _coven.Ritual<Empty>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ritual failed");
        }

        try
        {
            await Task.WhenAny(adapterTask, Task.Delay(Timeout.Infinite, stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
