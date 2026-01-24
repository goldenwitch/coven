// SPDX-License-Identifier: BUSL-1.1

using Coven.Covenants.Tests.Infrastructure;
using Xunit;

namespace Coven.Covenants.Tests;

/// <summary>
/// Tests for <see cref="JunctionBuilder{TCovenant, TIn}"/>.
/// </summary>
public class JunctionBuilderTests
{
    [Fact]
    public void RouteWithValidPredicateAndTransformAddsRoute()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        builder.Route<IntermediateEntry>(
            predicate: _ => true,
            transform: s => new IntermediateEntry());

        Assert.Single(builder.Routes);
        Assert.Equal(typeof(IntermediateEntry), builder.Routes[0].OutputType);
        Assert.NotNull(builder.Routes[0].Predicate);
        Assert.False(builder.Routes[0].IsMany);
    }

    [Fact]
    public void RouteManyWithValidPredicateAndTransformAddsRouteWithIsManyTrue()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        builder.RouteMany<IntermediateEntry>(
            predicate: _ => true,
            transform: s => [new IntermediateEntry()]);

        Assert.Single(builder.Routes);
        Assert.True(builder.Routes[0].IsMany);
    }

    [Fact]
    public void FallbackWithValidTransformSetsFallbackRoute()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        builder.Fallback<SinkEntry>(s => new SinkEntry());

        Assert.NotNull(builder.FallbackRoute);
        Assert.Equal(typeof(SinkEntry), builder.FallbackRoute.OutputType);
        Assert.Null(builder.FallbackRoute.Predicate);
    }

    [Fact]
    public void FallbackCalledTwiceThrowsInvalidOperation()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();
        builder.Fallback<SinkEntry>(s => new SinkEntry());

        Assert.Throws<InvalidOperationException>(() =>
            builder.Fallback<SinkEntry2>(s => new SinkEntry2()));
    }

    [Fact]
    public void RouteWithNullPredicateThrowsArgumentNull()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.Route<IntermediateEntry>(
                predicate: null!,
                transform: s => new IntermediateEntry()));
    }

    [Fact]
    public void RouteWithNullTransformThrowsArgumentNull()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.Route<IntermediateEntry>(
                predicate: _ => true,
                transform: null!));
    }

    [Fact]
    public void FallbackWithNullTransformThrowsArgumentNull()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.Fallback<SinkEntry>(null!));
    }

    [Fact]
    public void MultipleRoutesAccumulateInOrder()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        builder
            .Route<IntermediateEntry>(_ => true, _ => new IntermediateEntry())
            .Route<IntermediateEntry2>(_ => false, _ => new IntermediateEntry2())
            .RouteMany<IntermediateEntry3>(_ => true, _ => [new IntermediateEntry3()]);

        Assert.Equal(3, builder.Routes.Count);
        Assert.Equal(typeof(IntermediateEntry), builder.Routes[0].OutputType);
        Assert.Equal(typeof(IntermediateEntry2), builder.Routes[1].OutputType);
        Assert.Equal(typeof(IntermediateEntry3), builder.Routes[2].OutputType);
    }

    [Fact]
    public void FluentApiReturnsSameBuilder()
    {
        JunctionBuilder<TestCovenant, SourceEntry> builder = CreateBuilder();

        IJunctionBuilder<TestCovenant, SourceEntry> result1 = builder.Route<IntermediateEntry>(_ => true, _ => new IntermediateEntry());
        IJunctionBuilder<TestCovenant, SourceEntry> result2 = result1.RouteMany<IntermediateEntry2>(_ => true, _ => [new IntermediateEntry2()]);
        IJunctionBuilder<TestCovenant, SourceEntry> result3 = result2.Fallback<SinkEntry>(_ => new SinkEntry());

        Assert.Same(builder, result1);
        Assert.Same(builder, result2);
        Assert.Same(builder, result3);
    }

    private static JunctionBuilder<TestCovenant, SourceEntry> CreateBuilder() => new();
}
