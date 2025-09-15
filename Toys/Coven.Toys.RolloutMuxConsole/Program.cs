// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Tail;
using System.Text;

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

        // Console key pump -> Codex stdin (raw, with escape-hatch)
        Task inputTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                ConsoleKeyInfo key;
                try { key = Console.ReadKey(intercept: true); }
                catch (InvalidOperationException) { cts.Cancel(); break; } // input stream closed

                // Escape hatch: backtick for command mode, double backtick to send literal backtick
                if (key.KeyChar == '`' && key.Modifiers == 0)
                {
                    ConsoleKeyInfo next;
                    try { next = Console.ReadKey(intercept: true); }
                    catch (InvalidOperationException) { cts.Cancel(); break; }

                    if (next.KeyChar == '`' && next.Modifiers == 0)
                    {
                        await SafeWriteAsync(send, "`", cts.Token).ConfigureAwait(false);
                        continue;
                    }

                    Console.WriteLine();
                    Console.Write("`> ");
                    var cmd = new StringBuilder();
                    // Seed command with the second key if printable (non-control, excluding modifiers except Shift)
                    if (next.KeyChar != '\0' && (next.Modifiers & ~ConsoleModifiers.Shift) == 0 && !char.IsControl(next.KeyChar))
                    {
                        cmd.Append(next.KeyChar);
                        Console.Write(next.KeyChar);
                    }

                    while (true)
                    {
                        ConsoleKeyInfo k = Console.ReadKey(intercept: true);
                        if (k.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine();
                            break;
                        }
                        if (k.Key == ConsoleKey.Escape)
                        {
                            Console.WriteLine();
                            cmd.Clear();
                            break;
                        }
                        if (k.Key == ConsoleKey.Backspace)
                        {
                            if (cmd.Length > 0)
                            {
                                cmd.Length -= 1;
                                Console.Write("\b \b");
                            }
                            continue;
                        }
                        if (k.KeyChar != '\0' && (k.Modifiers & ~ConsoleModifiers.Shift) == 0 && !char.IsControl(k.KeyChar))
                        {
                            cmd.Append(k.KeyChar);
                            Console.Write(k.KeyChar);
                            continue;
                        }
                    }

                    var command = cmd.ToString().Trim();
                    if (command.Length > 0)
                    {
                        switch (command.ToLowerInvariant())
                        {
                            case "exit":
                            case "ctrlc":
                                await SafeWriteAsync(send, "\u0003", cts.Token).ConfigureAwait(false); // ETX
                                break;
                            case "help":
                                Console.WriteLine("Commands: help, exit|ctrlc (send Ctrl+C), quit (exit mux)");
                                break;
                            case "quit":
                                cts.Cancel();
                                break;
                            default:
                                Console.WriteLine($"Unknown command: {command}");
                                break;
                        }
                    }
                    continue;
                }

                // Normal key mapping
                if (KeyMapper.TryMap(key, out var seq) && seq is not null)
                {
                    await SafeWriteAsync(send, seq, cts.Token).ConfigureAwait(false);
                }
            }
        }, cts.Token);

        // Print small help
        Console.WriteLine("RolloutMuxConsole ready.");
        Console.WriteLine("- Raw key passthrough enabled (arrows, etc.)");
        Console.WriteLine("- Press ` then type a command (help, exit, quit)");
        Console.WriteLine("- Ctrl+C exits the mux; use `exit to send Ctrl+C to child");

        try { await Task.WhenAll(tailTask, inputTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return 0;
        }

    private static async Task SafeWriteAsync(ProcessSendPort send, string data, CancellationToken token)
    {
        try { await send.WriteAsync(data, token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (FileNotFoundException fnf)
        {
            Console.Error.WriteLine($"[codex-missing] {fnf.Message}");
        }
        catch (DirectoryNotFoundException dnf)
        {
            Console.Error.WriteLine($"[workspace-invalid] {dnf.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[write-error] {ex.Message}");
        }
    }
    }
