using Coven.Chat.Adapter.Discord;
using Coven.Chat.Journal;
using Xunit;

namespace Coven.Chat.Adapter.Discord.Tests;

public class DiscordReaderMappingTests
{
    [Fact]
    public void AskIncludesCallIdAndOptionsInMeta()
    {
        var call = Guid.NewGuid();
        var ask = new AskEntry(call, new HumanAsk("proceed?", new[] { "Yes", "No" }), DateTimeOffset.UtcNow);
        var change = DiscordChatJournalReader.Map(ask)!;
        Assert.Equal("ask", change.RenderKind);
        Assert.NotNull(change.Meta);
        Assert.Equal(call.ToString("N"), change.Meta!["callId"]);
        Assert.Equal("Yes,No", change.Meta!["options"]);
    }
}

