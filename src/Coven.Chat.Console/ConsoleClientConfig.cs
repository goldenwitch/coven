// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Console;

/// <summary>
/// Minimal configuration required by the Console chat adapter.
/// </summary>
public sealed class ConsoleClientConfig
{
    /// <summary>
    /// Gets or sets the sender label used for stdin messages.
    /// </summary>
    public required string InputSender { get; init; }

    /// <summary>
    /// Gets or sets the sender label used for stdout messages.
    /// </summary>
    public required string OutputSender { get; init; }
}

