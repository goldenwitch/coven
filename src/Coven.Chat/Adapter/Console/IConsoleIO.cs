// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Adapter.Console;

using System.Threading;
using System.Threading.Tasks;

public interface IConsoleIO
{
    Task<string?> ReadLineAsync(CancellationToken ct = default);
    Task WriteLineAsync(string line, CancellationToken ct = default);
}
