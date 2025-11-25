// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Chat.Console;

/// <summary>
/// Maps between Console-specific entries and generic Chat entries.
/// Afferent: Console → Chat; Efferent: Chat → Console.
/// </summary>
internal sealed class ConsoleTransmuter(ConsoleClientConfig config) : IBiDirectionalTransmuter<ConsoleEntry, ChatEntry>
{
    private readonly ConsoleClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    public Task<ChatEntry> TransmuteAfferent(ConsoleEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ConsoleAfferent incoming => Task.FromResult<ChatEntry>(new ChatAfferent(incoming.Sender, incoming.Text)),
            ConsoleEfferent outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, "ACK" + outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<ConsoleEntry> TransmuteEfferent(ChatEntry Output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Output switch
        {
            ChatEfferent outgoing => Task.FromResult<ConsoleEntry>(new ConsoleEfferent(_config.OutputSender, outgoing.Text)),

            // Internal/unfixed artifacts or inbound: acknowledge only
            ChatEfferentDraft draft => Task.FromResult<ConsoleEntry>(new ConsoleAck(draft.Sender, draft.Text)),
            ChatChunk chunk => Task.FromResult<ConsoleEntry>(new ConsoleAck(chunk.Sender, chunk.Text)),
            ChatStreamCompleted done => Task.FromResult<ConsoleEntry>(new ConsoleAck(done.Sender, string.Empty)),
            ChatAfferent incoming => Task.FromResult<ConsoleEntry>(new ConsoleAck(incoming.Sender, incoming.Text)),
            ChatAfferentDraft incomingDraft => Task.FromResult<ConsoleEntry>(new ConsoleAck(incomingDraft.Sender, incomingDraft.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
