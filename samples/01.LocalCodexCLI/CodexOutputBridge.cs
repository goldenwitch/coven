using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Chat;

namespace Coven.Samples.LocalCodexCLI;

// Bridges Codex string output into the chat journal as ChatResponse entries
internal sealed class CodexOutputBridge : BackgroundService
{
    private readonly IScrivener<string> _codexOutput;
    private readonly IScrivener<ChatEntry> _chat;
    private readonly ILogger<CodexOutputBridge> _logger;

    public CodexOutputBridge(
        IScrivener<string> codexOutput,
        IScrivener<ChatEntry> chat,
        ILogger<CodexOutputBridge> logger)
    {
        _codexOutput = codexOutput;
        _chat = chat;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CodexOutputBridge running.");

        long after = 0;
        await foreach ((long pos, string line) in _codexOutput.TailAsync(after, stoppingToken).ConfigureAwait(false))
        {
            after = pos;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var text = line.TrimEnd();
            var response = new ChatResponse("codex", text);
            try
            {
                _ = await _chat.WriteAsync(response, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

