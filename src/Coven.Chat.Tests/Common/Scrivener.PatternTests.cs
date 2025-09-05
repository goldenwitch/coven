using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Coven.Chat.Tests;

// Scope: Common pattern tests from the design doc (A, B), using a simple flow entry hierarchy.
public static class FlowScrivenerFactories
{
    public static IEnumerable<object[]> Create()
    {
        yield return new object[] { new Func<IScrivener<FlowEntry>>(() => new InMemoryScrivener<FlowEntry>()) };
        // Add new IScrivener<FlowEntry> implementations here as factories.
    }
}

public abstract record FlowEntry;
public sealed record FlowUser(string Text) : FlowEntry;
public sealed record FlowReply(string Text) : FlowEntry;
public sealed record FlowCompleted(object? Result) : FlowEntry;
public sealed record FlowError(string Message) : FlowEntry;
public sealed record FlowAsk(string Prompt) : FlowEntry;
public sealed record FlowAnswer(string Selection) : FlowEntry;

public class ScrivenerPatternTests
{
    [Theory]
    [MemberData(nameof(FlowScrivenerFactories.Create), MemberType = typeof(FlowScrivenerFactories))]
    public async Task RequestReply_ReturnsFirstTerminalAfterAnchor(Func<IScrivener<FlowEntry>> create)
    {
        var s = create();
        var anchor = await s.WriteAsync(new FlowUser("hello"));

        // Write a terminal entry (reply). Test that WaitFor anchored at the user returns it.
        var wait = s.WaitForAsync(anchor, e => e is FlowReply || e is FlowCompleted || e is FlowError);
        var pr = await s.WriteAsync(new FlowReply("hi"));
        var (pos, entry) = await wait;

        Assert.Equal(pr, pos);
        Assert.IsType<FlowReply>(entry);
    }

    [Theory]
    [MemberData(nameof(FlowScrivenerFactories.Create), MemberType = typeof(FlowScrivenerFactories))]
    public async Task AskAnswer_AnswerAfterWait_Matches(Func<IScrivener<FlowEntry>> create)
    {
        var s = create();
        var pAsk = await s.WriteAsync(new FlowAsk("ok?"));
        var wait = s.WaitForAsync<FlowAnswer>(pAsk);
        var pa = await s.WriteAsync(new FlowAnswer("Yes"));
        var (pos, ans) = await wait;
        Assert.Equal(pa, pos);
        Assert.Equal("Yes", ans.Selection);
    }

    [Theory]
    [MemberData(nameof(FlowScrivenerFactories.Create), MemberType = typeof(FlowScrivenerFactories))]
    public async Task AskAnswer_AnswerBeforeWait_Matches(Func<IScrivener<FlowEntry>> create)
    {
        var s = create();
        var pAsk = await s.WriteAsync(new FlowAsk("ok?"));
        var pa = await s.WriteAsync(new FlowAnswer("Yes"));
        var (pos, ans) = await s.WaitForAsync<FlowAnswer>(pAsk);
        Assert.Equal(pa, pos);
        Assert.Equal("Yes", ans.Selection);
    }
}

