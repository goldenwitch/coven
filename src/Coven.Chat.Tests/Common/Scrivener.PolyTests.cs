// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Coven.Chat.Tests.TestTooling;

namespace Coven.Chat.Tests;

// Scope: Common behavior tests for derived-type waiting that must hold for ANY IScrivener<T>.
public static class PolyScrivenerFactories
{
    public static IEnumerable<object[]> Create()
        => ScrivenerFactory.Both<TestBaseEntry>("Coven_FileScrivener_Common_Poly_");
}

// Simple polymorphic entry hierarchy for derived-type tests
public abstract record TestBaseEntry;
public sealed record TestDerivedA(string Text) : TestBaseEntry;
public sealed record TestDerivedB(int Number) : TestBaseEntry;

public class ScrivenerPolyTests
{
    [Theory]
    [MemberData(nameof(PolyScrivenerFactories.Create), MemberType = typeof(PolyScrivenerFactories))]
    public async Task WaitForAsync_DerivedTypeMatches(Func<IScrivener<TestBaseEntry>> create)
    {
        var s = create();
        var anchor = await s.WriteAsync(new TestDerivedA("x"));
        var waiter = s.WaitForAsync<TestDerivedB>(anchor);
        await s.WriteAsync(new TestDerivedA("y"));
        var pb = await s.WriteAsync(new TestDerivedB(42));
        var (pos, entry) = await waiter;
        Assert.Equal(pb, pos);
        Assert.IsType<TestDerivedB>(entry);
        Assert.Equal(42, entry.Number);
    }
}