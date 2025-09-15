// SPDX-License-Identifier: BUSL-1.1

using System.Text;
using Coven.Spellcasting.Agents;

namespace Coven.Toys.RolloutMuxConsole;

/// <summary>
/// Handles console input and forwards mapped sequences to the child process stdin.
/// Provides an escape-hatch command mode using a configurable escape key.
/// Separated from Program.cs to keep responsibilities clean.
/// </summary>
internal sealed class ConsoleKeyPump
{
    private readonly ISendPort _send;
    private readonly char _escape;

    public ConsoleKeyPump(ISendPort send, char escape)
    {
        _send = send;
        _escape = escape;
    }

    public Task RunAsync(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                ConsoleKeyInfo key;
                try { key = Console.ReadKey(intercept: true); }
                catch (InvalidOperationException) { break; } // input stream closed
                KeyDebugEcho.Raw(key);

                // Escape hatch: escape for command mode, double escape for literal passthrough
                if (key.KeyChar == _escape && key.Modifiers == 0)
                {
                    ConsoleKeyInfo next;
                    try { next = Console.ReadKey(intercept: true); }
                    catch (InvalidOperationException) { break; }

                    if (next.KeyChar == _escape && next.Modifiers == 0)
                    {
                        KeyDebugEcho.Info("literal-escape");
                        await _send.SafeWriteAsync(_escape.ToString(), token).ConfigureAwait(false);
                        continue;
                    }

                    Console.WriteLine();
                    Console.Write($"{_escape}> ");
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
                            // Escape passthrough: if no command typed yet, send ESC to child
                            if (cmd.Length == 0)
                            {
                                KeyDebugEcho.Info("send: ESC");
                                await _send.SafeWriteAsync("\u001b", token).ConfigureAwait(false);
                            }
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
                                KeyDebugEcho.Info("send: CTRL+C");
                                await _send.SafeWriteAsync("\u0003", token).ConfigureAwait(false); // ETX
                                break;
                            case "help":
                                Console.WriteLine("Commands: help, exit|ctrlc (send Ctrl+C), quit (exit mux)");
                                break;
                            case "quit":
                                return; // exit pump
                            default:
                                Console.WriteLine($"Unknown command: {command}");
                                break;
                        }
                    }
                    continue;
                }

                // Normal key mapping
                if (KeyMapper.TryMap(key, out var seq))
                {
                    KeyDebugEcho.Mapped(key, seq);
                    await _send.SafeWriteAsync(seq, token).ConfigureAwait(false);
                }
            }
        }, token);
    }
}

