using System.Collections.Concurrent;
using Coven.Chat;
using Coven.Chat.Adapter.Discord;
using Xunit;

namespace Coven.Chat.Adapter.Discord.Tests;

file sealed class FakeDiscordApi : IDiscordApi
{
    public int Sends; public int Edits;
    public List<(string place, string text)> SendCalls { get; } = new();
    public List<(string place, string messageId, string text)> EditCalls { get; } = new();
    public Task<DiscordMessage> SendAsync(string placeId, string text, DiscordComponents? components, CancellationToken ct)
    {
        Sends++; SendCalls.Add((placeId, text));
        return Task.FromResult(new DiscordMessage(Guid.NewGuid().ToString("N")));
    }
    public Task<DiscordMessage> EditAsync(string placeId, string messageId, string text, DiscordComponents? components, CancellationToken ct)
    {
        Edits++; EditCalls.Add((placeId, messageId, text));
        return Task.FromResult(new DiscordMessage(messageId));
    }
}

public class DiscordDeliveryTests
{
    private static readonly TranscriptRef Where = new("discord:bot", "channel1", "root1");

    [Fact]
    public async Task IdempotencyPreventsDuplicates()
    {
        var api = new FakeDiscordApi();
        var delivery = new DiscordDelivery(api);
        var change = new OutboundChange(DeliveryMode.Append, "hello");
        var key = "corr:1";
        await delivery.ApplyAsync(Where, change, key, default);
        await delivery.ApplyAsync(Where, change, key, default);
        Assert.Equal(1, api.Sends);
    }

    [Fact]
    public async Task UpdateFallsBackToAppendWhenNoPriorMessage()
    {
        var api = new FakeDiscordApi();
        var delivery = new DiscordDelivery(api);
        var change = new OutboundChange(DeliveryMode.Update, "progress", UpdateKey: "progress", RenderKind: "progress");
        await delivery.ApplyAsync(Where, change, "corr:2", default);
        Assert.Equal(1, api.Sends);
        Assert.Equal(0, api.Edits);
    }

    [Fact]
    public async Task UpdateEditsWhenPriorMessageExists()
    {
        var api = new FakeDiscordApi();
        var delivery = new DiscordDelivery(api);
        var upKey = "progress";
        var append = new OutboundChange(DeliveryMode.Append, "p1", UpdateKey: upKey, RenderKind: "progress");
        var update = new OutboundChange(DeliveryMode.Update, "p2", UpdateKey: upKey, RenderKind: "progress");
        await delivery.ApplyAsync(Where, append, "corr:3", default);
        await delivery.ApplyAsync(Where, update, "corr:4", default);
        Assert.Equal(1, api.Sends);
        Assert.Equal(1, api.Edits);
        Assert.Equal("p2", api.EditCalls.Last().text);
    }
}

