// SPDX-License-Identifier: BUSL-1.1

using Coven.Covenants.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Covenants.Tests;

/// <summary>
/// Tests for <see cref="StreamingCovenantBuilder{TCovenant}"/>.
/// </summary>
public class StreamingCovenantBuilderTests
{
    [Fact]
    public void SourceAddsSourceEdgeToGraph()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        builder.Source<SourceEntry>();

        Assert.Single(builder.Graph.Sources);
        Assert.Equal(typeof(SourceEntry), builder.Graph.Sources.First());
    }

    [Fact]
    public void SinkAddsSinkEdgeToGraph()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        builder.Sink<SinkEntry>();

        Assert.Single(builder.Graph.Sinks);
        Assert.Equal(typeof(SinkEntry), builder.Graph.Sinks.First());
    }

    [Fact]
    public void JunctionWithNoRoutesThrowsInvalidOperation()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        Assert.Throws<InvalidOperationException>(() =>
            builder.Junction<SourceEntry>(_ => { /* no routes */ }));
    }

    [Fact]
    public void JunctionWithRoutesAddsJunctionEdge()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        builder.Junction<SourceEntry>(j => j
            .Route<IntermediateEntry>(_ => true, _ => new IntermediateEntry())
            .Fallback<SinkEntry>(_ => new SinkEntry()));

        List<CovenantJunctionEdge> junctions = [.. builder.Graph.Junctions];
        Assert.Single(junctions);
        Assert.Equal(typeof(SourceEntry), junctions[0].InputType);
        Assert.Single(junctions[0].Routes);
        Assert.NotNull(junctions[0].FallbackRoute);
    }

    [Fact]
    public void JunctionConfigureThrowsPropagates()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        Assert.Throws<InvalidOperationException>(() =>
            builder.Junction<SourceEntry>(_ => throw new InvalidOperationException("Test")));
    }

    [Fact]
    public void JunctionWithNullConfigureThrowsArgumentNull()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        Assert.Throws<ArgumentNullException>(() =>
            builder.Junction<SourceEntry>(null!));
    }

    [Fact]
    public void ValidateWithValidGraphSucceeds()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);
        builder
            .Source<SourceEntry>()
            .Sink<SinkEntry>()
            .Junction<SourceEntry>(static j => j.Fallback<SinkEntry>(_ => new SinkEntry()));

        // Act & Assert: Should not throw
        builder.Validate();
    }

    [Fact]
    public void ValidateWithInvalidGraphThrowsInvalidOperation()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);
        builder.Source<SourceEntry>();  // Missing sink

        Assert.Throws<InvalidOperationException>(builder.Validate);
    }

    [Fact]
    public void FluentApiReturnsSameBuilder()
    {
        ServiceCollection services = new();
        StreamingCovenantBuilder<TestCovenant> builder = new(services);

        IStreamingCovenantBuilder<TestCovenant> result1 = builder.Source<SourceEntry>();
        IStreamingCovenantBuilder<TestCovenant> result2 = result1.Sink<SinkEntry>();

        Assert.Same(builder, result1);
        Assert.Same(builder, result2);
    }

    [Fact]
    public void ConstructorWithNullServicesThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StreamingCovenantBuilder<TestCovenant>(null!));
    }
}
