using Coven.Chat.Adapter.Discord;
using Coven.Chat.Journal;
using Xunit;

namespace Coven.Chat.Adapter.Discord.Tests;

public class DiscordIngressTests
{
    [Fact]
    public async Task ComponentClickAppendsHumanResponseAndCompletesAwaitable()
    {
        var store = new InMemoryAgentJournalStore();
        var ckpt = new InMemoryCheckpointStore();
        var barrier = new DefaultJournalBarrier(ckpt);
        var waiter = new DefaultJournalWaiter(store);
        var corr = Guid.NewGuid();
        var journal = new DefaultMagikJournal(store, barrier, waiter, corr);

        var ask = await journal.Ask(new HumanAsk("ok?", new[] { "Yes", "No" }));
        var callId = ask.CallId.ToString("N");

        var ingress = new DiscordIngress(store, _ => corr);
        var interaction = new DiscordComponentInteraction(new("user1","Tester"), new($"coven|ask|{callId}|Yes"));
        var handled = await ingress.TryHandleComponentAsync(interaction);
        Assert.True(handled);

        var resp = await ask.Response(TimeSpan.FromSeconds(1));
        Assert.Equal("Yes", resp.Selected);
        Assert.NotNull(resp.Fields);
        Assert.Equal("user1", resp.Fields!["responderId"]);
    }
}

