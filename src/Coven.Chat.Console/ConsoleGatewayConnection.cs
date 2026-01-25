// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

internal sealed class ConsoleGatewayConnection(
    IConsoleIO consoleIO,
    ConsoleClientConfig configuration,
    [FromKeyedServices("Coven.InternalConsoleScrivener")] IScrivener<ConsoleEntry> scrivener,
    ILogger<ConsoleGatewayConnection> logger)
{
    private readonly IConsoleIO _consoleIO = consoleIO ?? throw new ArgumentNullException(nameof(consoleIO));
    private readonly ConsoleClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<ConsoleEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private Task? _stdinPump;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        ConsoleLog.Connecting(_logger);

        // Start a background pump that reads stdin line-by-line and appends ConsoleIncoming entries.
        _stdinPump = Task.Run(async () =>
        {
            CancellationToken ct = cancellationToken;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                string? line;
                try
                {
                    line = await _consoleIO.ReadLineAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line is null)
                {
                    // EOF; end pump cooperatively.
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    ConsoleLog.InboundEmptySkipped(_logger);
                    continue;
                }

                string sender = _configuration.InputSender;
                ConsoleAfferent incoming = new(sender, line);
                ConsoleLog.InboundUserLineReceived(_logger, sender, line.Length);
                long pos = await _scrivener.WriteAsync(incoming, ct).ConfigureAwait(false);
                ConsoleLog.InboundAppendedToJournal(_logger, nameof(ConsoleAfferent), pos);
            }
        }, cancellationToken);

        ConsoleLog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ConsoleLog.OutboundSendStart(_logger, text.Length);
        try
        {
            await _consoleIO.WriteLineAsync(text, cancellationToken).ConfigureAwait(false);
            ConsoleLog.OutboundSendSucceeded(_logger);
        }
        catch (OperationCanceledException)
        {
            ConsoleLog.OutboundOperationCanceled(_logger);
            throw;
        }
        catch (Exception ex)
        {
            ConsoleLog.OutboundSendFailed(_logger, ex);
            throw;
        }
    }

    public async Task DrainAsync()
    {
        if (_stdinPump is not null)
        {
            try
            {
                await _stdinPump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cooperative cancellation.
            }
        }
    }
}
