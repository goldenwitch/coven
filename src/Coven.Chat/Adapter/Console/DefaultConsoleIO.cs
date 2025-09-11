// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Adapter.Console;

using System.Threading;
using System.Threading.Tasks;

public sealed class DefaultConsoleIO : IConsoleIO
{
    public Task<string?> ReadLineAsync(CancellationToken ct = default)
        => System.Console.In.ReadLineAsync(ct).AsTask();

    public Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        // System.Console.Out.WriteLineAsync has no CancellationToken; emulate best-effort.
        return System.Console.Out.WriteLineAsync(line);
    }
}