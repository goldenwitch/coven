// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Tail;

namespace Coven.Toys.RolloutMuxConsole;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string exe = "codex";
        string ws  = Directory.GetCurrentDirectory();
        await using CodexSessionScope session = new(ws);

        // Environment to match CodexCliAgent behavior per scratch.txt
        Dictionary<string, string?> env = new()
        {
            ["CODEX_HOME"] = session.CodexHome,
            ["CODEX_TUI_RECORD_SESSION"] = "1",
            ["CODEX_TUI_SESSION_LOG_PATH"] = session.RolloutPath
        };

        await using ProcessSendPort send = new(
            fileName: exe,
            arguments: null,
            workingDirectory: ws,
            environment: env);

        await using DocumentTailSource tail = new(session.RolloutPath);

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Tail Codex rollout -> console
        Task tailTask = tail.TailAsync(ev =>
        {
            switch (ev)
            {
                case Line o:
                    Console.WriteLine(o.Line);
                    break;
                case ErrorLine e:
                    Console.Error.WriteLine(e.Line);
                    break;
            }
            return ValueTask.CompletedTask;
        }, cts.Token);

        // Console input -> Codex stdin
        Task inputTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                string? line;
                try { line = await Console.In.ReadLineAsync().WaitAsync(cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (line is null) { cts.Cancel(); break; } // EOF -> cancel tail

                try { await send.WriteLineAsync(line, cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (FileNotFoundException fnf)
                {
                    Console.Error.WriteLine($"[codex-missing] {fnf.Message}");
                    cts.Cancel();
                    break;
                }
                catch (DirectoryNotFoundException dnf)
                {
                    Console.Error.WriteLine($"[workspace-invalid] {dnf.Message}");
                    cts.Cancel();
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[write-error] {ex.Message}");
                    cts.Cancel();
                    break;
                }
            }
        }, cts.Token);

        // Print small help
        Console.WriteLine("RolloutMuxConsole ready. Ctrl+C to exit.");

        try { await Task.WhenAll(tailTask, inputTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return 0;
    }
}
