// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coven.Toys.MockProcess;

internal sealed class MockProcessOrchestrator : BackgroundService
{
    private readonly ILogger<MockProcessOrchestrator> _log;
    public MockProcessOrchestrator(ILogger<MockProcessOrchestrator> log) { _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            _log.LogInformation("MockProcess starting");

            var rolloutPath = ResolveRolloutPath(args);
            Directory.CreateDirectory(Path.GetDirectoryName(rolloutPath)!);
            _log.LogInformation("MockProcess rollout path: {Path}", rolloutPath);

            await using var fs = new FileStream(
                rolloutPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
            await using var writer = new StreamWriter(fs) { AutoFlush = true };

            var sessionId = $"mock-{Guid.NewGuid():N}";
            await WriteJsonLineAsync(writer, new
            {
                type = "metadata",
                created = DateTimeOffset.UtcNow.ToString("O"),
                session_id = sessionId,
                cwd = Directory.GetCurrentDirectory()
            });
            _log.LogInformation("metadata written: session={SessionId}", sessionId);

            await using var stdin = Console.OpenStandardInput();
            using var reader = new StreamReader(stdin);
            while (!stoppingToken.IsCancellationRequested)
            {
                string? line;
                try { line = await StdInLineReader.ReadLineAsync(reader, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (line is null) break; // stdin closed
                _log.LogInformation("stdin line: {Line}", line);

                if (string.Equals(line, "exit", StringComparison.OrdinalIgnoreCase))
                    break;
                if (string.Equals(line, "error", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonLineAsync(writer, new
                    {
                        type = "error",
                        created = DateTimeOffset.UtcNow.ToString("O"),
                        message = "mock error signaled by input",
                        code = "mock_error"
                    });
                    _log.LogInformation("error written");
                    continue;
                }

                await WriteJsonLineAsync(writer, new
                {
                    type = "message",
                    created = DateTimeOffset.UtcNow.ToString("O"),
                    role = "assistant",
                    content = $"echo: {line}"
                });
                _log.LogInformation("message written: {Line}", line);
            }
            _log.LogInformation("MockProcess shutdown");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogError(ex, "MockProcess failed");
            try
            {
                var fallback = Path.Combine(Directory.GetCurrentDirectory(), "codex.rollout.jsonl");
                await File.AppendAllTextAsync(fallback, JsonSerializer.Serialize(new
                {
                    type = "error",
                    created = DateTimeOffset.UtcNow.ToString("O"),
                    message = ex.Message,
                    detail = ex.ToString(),
                    code = ex.GetType().Name
                }) + Environment.NewLine);
            }
            catch { }
        }
    }

    private static string ResolveRolloutPath(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--rollout" || args[i] == "-r") && i + 1 < args.Length)
            {
                var p = args[i + 1];
                if (!string.IsNullOrWhiteSpace(p)) return Path.GetFullPath(p);
            }
        }
        var env = Environment.GetEnvironmentVariable("MOCK_ROLLOUT_PATH");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
        return Path.Combine(Directory.GetCurrentDirectory(), "codex.rollout.jsonl");
    }

    private static async Task WriteJsonLineAsync(StreamWriter writer, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        await writer.WriteLineAsync(json);
        await writer.FlushAsync();
    }
}
