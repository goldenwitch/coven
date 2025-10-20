using Coven.Transmutation;

namespace Coven.Chat.Console;

public sealed class ConsoleTransmuter(ConsoleClientConfig config) : IBiDirectionalTransmuter<ConsoleEntry, ChatEntry>
{
    private readonly ConsoleClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    public Task<ChatEntry> TransmuteIn(ConsoleEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ConsoleIncoming incoming => Task.FromResult<ChatEntry>(new ChatIncoming(incoming.Sender, incoming.Text)),
            ConsoleOutgoing outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<ConsoleEntry> TransmuteOut(ChatEntry Output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Output switch
        {
            ChatIncoming incoming => Task.FromResult<ConsoleEntry>(new ConsoleAck(incoming.Sender, incoming.Text)),
            ChatOutgoing outgoing => Task.FromResult<ConsoleEntry>(new ConsoleOutgoing(_config.OutputSender, outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
