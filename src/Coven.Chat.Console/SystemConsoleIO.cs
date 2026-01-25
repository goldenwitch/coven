// SPDX-License-Identifier: BUSL-1.1

using SysConsole = System.Console;

namespace Coven.Chat.Console;

/// <summary>
/// Production implementation of <see cref="IConsoleIO"/> that wraps <see cref="System.Console"/>.
/// </summary>
internal sealed class SystemConsoleIO : IConsoleIO
{
    /// <inheritdoc />
    public TextReader Input => SysConsole.In;

    /// <inheritdoc />
    public TextWriter Output => SysConsole.Out;

    /// <inheritdoc />
    public TextWriter ErrorOutput => SysConsole.Error;

    /// <inheritdoc />
    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        => await SysConsole.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public ValueTask WriteLineAsync(string value, CancellationToken cancellationToken)
    {
        // Console.WriteLine is synchronous; wrap with WaitAsync for cancellation support
        SysConsole.WriteLine(value);
        return ValueTask.CompletedTask;
    }
}
