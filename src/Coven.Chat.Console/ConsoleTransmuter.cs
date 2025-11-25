// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Chat.Console;

/// <summary>
/// Maps between Console-specific entries and generic Chat entries.
/// Supports imbuing with the source record position for position-based ACKs.
/// </summary>
internal sealed class ConsoleTransmuter(ConsoleClientConfig config)
    : IImbuingTransmuter<ConsoleEntry, long, ChatEntry>,
      IImbuingTransmuter<ChatEntry, long, ConsoleEntry>
{
    private readonly ConsoleClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    // Console → Chat (afferent):
    // - ConsoleAfferent -> ChatAfferent (position ignored)
    // - ConsoleEfferent -> ChatAck(position)
    public Task<ChatEntry> Transmute(ConsoleEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ConsoleAfferent incoming => Task.FromResult<ChatEntry>(new ChatAfferent(incoming.Sender, incoming.Text)),
            ConsoleEfferent outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, Reagent)),
            ConsoleAck => Task.FromResult<ChatEntry>(new ChatAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    // Chat → Console (efferent):
    // - ChatEfferent -> ConsoleEfferent
    // - All others -> ConsoleAck(position)
    public Task<ConsoleEntry> Transmute(ChatEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ChatEfferent outgoing => Task.FromResult<ConsoleEntry>(new ConsoleEfferent(_config.OutputSender, outgoing.Text)),

            ChatEfferentDraft draft => Task.FromResult<ConsoleEntry>(new ConsoleAck(draft.Sender, Reagent)),
            ChatChunk chunk => Task.FromResult<ConsoleEntry>(new ConsoleAck(chunk.Sender, Reagent)),
            ChatStreamCompleted done => Task.FromResult<ConsoleEntry>(new ConsoleAck(done.Sender, Reagent)),
            ChatAfferent incoming => Task.FromResult<ConsoleEntry>(new ConsoleAck(incoming.Sender, Reagent)),
            ChatAfferentDraft incomingDraft => Task.FromResult<ConsoleEntry>(new ConsoleAck(incomingDraft.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
