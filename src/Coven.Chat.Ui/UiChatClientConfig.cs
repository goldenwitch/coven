// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Ui;

/// <summary>
/// Minimal configuration required by the UI chat adapter.
/// </summary>
public sealed class UiChatClientConfig
{
    /// <summary>
    /// Gets the sender label applied to messages submitted by the user.
    /// </summary>
    public required string InputSender { get; init; }

    /// <summary>
    /// Gets the sender label applied to messages rendered back to the user.
    /// </summary>
    public required string OutputSender { get; init; }
}
