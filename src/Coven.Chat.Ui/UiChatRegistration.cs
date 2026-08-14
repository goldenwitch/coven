// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Ui;

/// <summary>
/// Registration options for the UI chat adapter.
/// </summary>
public sealed class UiChatRegistration
{
    /// <summary>
    /// Gets a value indicating whether the adapter renders streaming chunks.
    /// </summary>
    public bool StreamingEnabled { get; private set; }

    /// <summary>
    /// Renders <see cref="ChatChunk"/> entries incrementally in addition to finalized messages.
    /// </summary>
    /// <remarks>
    /// Adds <see cref="ChatChunk"/> to the branch's consumed types, so the covenant must
    /// declare a route producing it.
    /// </remarks>
    /// <returns>The same registration for chaining.</returns>
    public UiChatRegistration EnableStreaming()
    {
        StreamingEnabled = true;
        return this;
    }
}
