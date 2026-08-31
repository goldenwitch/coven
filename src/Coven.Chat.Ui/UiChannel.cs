// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;

namespace Coven.Chat.Ui;

/// <summary>
/// Default <see cref="IUiChannel"/> implementation backed by an unbounded channel for
/// user input and an event for outbound payloads.
/// </summary>
/// <remarks>
/// <para>
/// The leaf consumes this through <see cref="IUiChannel"/>; a user interface drives it
/// through <see cref="SubmitAsync"/> and <see cref="Outbound"/>.
/// </para>
/// <para>
/// <see cref="Outbound"/> is raised on the thread that produced the payload — a background
/// journal pump, never a UI thread. Handlers must marshal to their own dispatcher.
/// </para>
/// </remarks>
public sealed class UiChannel : IUiChannel
{
    private readonly Channel<string> _input = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    /// Raised when the chat journal produces a payload for the user interface.
    /// </summary>
    public event Action<UiOutbound>? Outbound;

    /// <summary>
    /// Submits a user message toward the chat journal.
    /// </summary>
    /// <param name="text">The message text. Blank input is ignored.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the message is queued.</returns>
    public async ValueTask SubmitAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _input.Writer.WriteAsync(text, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the input side of the channel, ending the leaf's input pump cooperatively.
    /// </summary>
    public void Complete() => _input.Writer.TryComplete();

    /// <inheritdoc />
    public async ValueTask<string?> ReadInputAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _input.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(UiOutbound outbound, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outbound);
        cancellationToken.ThrowIfCancellationRequested();

        Outbound?.Invoke(outbound);
        return ValueTask.CompletedTask;
    }
}
