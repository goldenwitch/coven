// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Ui;

/// <summary>
/// Classifies an outbound payload so the UI can render finalized messages and
/// streaming fragments differently.
/// </summary>
public enum UiOutboundKind
{
    /// <summary>A finalized message.</summary>
    Message = 0,

    /// <summary>A streaming fragment that precedes a finalized message.</summary>
    Chunk = 1
}

/// <summary>
/// A payload travelling from the chat journal toward the UI.
/// </summary>
/// <param name="Kind">Whether the payload is a finalized message or a streaming fragment.</param>
/// <param name="Sender">Sender label to display.</param>
/// <param name="Text">Payload text.</param>
public sealed record UiOutbound(UiOutboundKind Kind, string Sender, string Text);

/// <summary>
/// In-process transport between the UI chat leaf and a user interface.
/// Implementations are UI-framework agnostic; marshalling onto a UI thread is
/// the responsibility of the consumer.
/// </summary>
public interface IUiChannel
{
    /// <summary>
    /// Waits for the next message submitted by the user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The submitted text, or <see langword="null"/> when the channel is closed.</returns>
    ValueTask<string?> ReadInputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers an outbound payload to the user interface.
    /// </summary>
    /// <param name="outbound">The payload to render.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the payload has been handed to the UI.</returns>
    ValueTask PublishAsync(UiOutbound outbound, CancellationToken cancellationToken = default);
}
