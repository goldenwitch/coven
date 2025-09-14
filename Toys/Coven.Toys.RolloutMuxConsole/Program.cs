// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Tail;

namespace Coven.Toys.RolloutMuxConsole;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var exe = EnvOrDefault("CODEX_EXE", "codex");
        var ws  = EnvOrDefault("CODEX_WORKSPACE", Directory.GetCurrentDirectory());
        var codexHome = Path.Combine(ws, ".codex");
        try { Directory.CreateDirectory(codexHome); } catch { }

        // Rollout path matches the Codex agent design
        var rolloutPath = Path.Combine(codexHome, "codex.rollout.jsonl");

        // Environment and args to mirror DefaultTailMuxFactory behavior
        var env = new Dictionary<string, string?> { ["CODEX_HOME"] = codexHome };
        var argsLine = $"--log-dir {codexHome}";

        await using var send = new ProcessSendPort(
            fileName: exe,
            arguments: argsLine,
            workingDirectory: ws,
            environment: env);

        await using var tail = new DocumentTailSource(rolloutPath);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Tail Codex rollout -> console
        var tailTask = Task.Run(async () =>
        {
            await tail.TailAsync(async ev =>
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
                await Task.CompletedTask;
            }, cts.Token);
        }, cts.Token);

        // Console input -> Codex stdin
        var inputTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                string? line;
                try { line = await Console.In.ReadLineAsync().WaitAsync(cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (line is null) break; // EOF

                try { await send.WriteLineAsync(line, cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (FileNotFoundException fnf)
                {
                    Console.Error.WriteLine($"[codex-missing] {fnf.Message}");
                    break;
                }
                catch (DirectoryNotFoundException dnf)
                {
                    Console.Error.WriteLine($"[workspace-invalid] {dnf.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[write-error] {ex.Message}");
                    break;
                }
            }
        }, cts.Token);

        // Print small help
        Console.WriteLine("RolloutMuxConsole ready. Ctrl+C to exit.");
        Console.WriteLine($"Workspace: {ws}");
        Console.WriteLine($"CodexHome: {codexHome}");
        Console.WriteLine($"Executable: {exe} {argsLine}");

        try { await Task.WhenAll(tailTask, inputTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return 0;
    }

    private static string EnvOrDefault(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? fallback : v!;
    }
}
