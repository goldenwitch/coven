// SPDX-License-Identifier: BUSL-1.1

using System.IO.Pipes;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            await Console.Error.WriteLineAsync("Usage: mcp-shim <pipeName>");
            return 2;
        }

        var pipeName = args[0];
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { try { cts.Cancel(); } catch { } };

            await client.ConnectAsync(cts.Token).ConfigureAwait(false);

            var stdin = Console.OpenStandardInput();
            var stdout = Console.OpenStandardOutput();

            // Bridge stdin -> pipe and pipe -> stdout concurrently.
            var t1 = PumpAsync(stdin, client, cts.Token);
            var t2 = PumpAsync(client, stdout, cts.Token);

            await Task.WhenAny(t1, t2).ConfigureAwait(false);
            try { cts.Cancel(); } catch { }
            await Task.WhenAll(Suppress(t1), Suppress(t2)).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            try { await Console.Error.WriteLineAsync(ex.ToString()); } catch { }
            return 1;
        }
    }

    private static async Task PumpAsync(Stream from, Stream to, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        while (!ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await from.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            if (read <= 0) break;
            await to.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            await to.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task Suppress(Task t)
    {
        try { await t.ConfigureAwait(false); } catch { }
    }
}
