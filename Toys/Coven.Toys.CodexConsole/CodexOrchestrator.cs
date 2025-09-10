using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Core;
using Coven.Chat;
using Coven.Chat.Adapter;

namespace Coven.Toys.CodexConsole;

internal sealed class CodexOrchestrator : BackgroundService
{
    private readonly ICoven _coven;
    private readonly IAdapterHost<ChatEntry> _adapterHost;
    private readonly IAdapter<ChatEntry> _adapter;
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly ILogger<CodexOrchestrator> _logger;

    public CodexOrchestrator(
        ICoven coven,
        IAdapterHost<ChatEntry> adapterHost,
        IAdapter<ChatEntry> adapter,
        IScrivener<ChatEntry> scrivener,
        ILogger<CodexOrchestrator> logger)
    {
        _coven = coven;
        _adapterHost = adapterHost;
        _adapter = adapter;
        _scrivener = scrivener;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CodexConsole toy started. Chat via ConsoleAdapter. Ctrl+C to exit.");

        Task adapterTask = _adapterHost.RunAsync(_scrivener, _adapter, stoppingToken);

        try
        {
            _logger.LogInformation("Starting ritual: validate + codex wizard.");
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
            // normal shutdown
        }
    }
}

