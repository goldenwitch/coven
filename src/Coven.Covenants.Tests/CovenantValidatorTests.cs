// SPDX-License-Identifier: BUSL-1.1

using Coven.Covenants.Tests.Infrastructure;
using Xunit;

namespace Coven.Covenants.Tests;

/// <summary>
/// Tests for <see cref="CovenantValidator"/>.
/// </summary>
public class CovenantValidatorTests
{
    #region Valid Graphs

    [Fact]
    public void ValidateMinimalValidGraphSucceeds()
    {
        // Arrange: Source -> Sink (direct transform)
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithTransform<SourceEntry, SinkEntry>()
            .Build();

        // Act & Assert: Should not throw
        CovenantValidator.Validate(graph);
    }

    [Fact]
    public void ValidateLinearChainSucceeds()
    {
        // Arrange: Source -> Intermediate -> Sink
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithTransform<SourceEntry, IntermediateEntry>()
            .WithTransform<IntermediateEntry, SinkEntry>()
            .Build();

        // Act & Assert
        CovenantValidator.Validate(graph);
    }

    [Fact]
    public void ValidateMultiHopChainSucceeds()
    {
        // Arrange: Source -> I1 -> I2 -> I3 -> Sink
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithTransform<SourceEntry, IntermediateEntry>()
            .WithTransform<IntermediateEntry, IntermediateEntry2>()
            .WithTransform<IntermediateEntry2, IntermediateEntry3>()
            .WithTransform<IntermediateEntry3, SinkEntry>()
            .Build();

        // Act & Assert
        CovenantValidator.Validate(graph);
    }

    [Fact]
    public void ValidateBranchingGraphSucceeds()
    {
        // Arrange: Source branches to two paths, both reaching a sink
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithSink<SinkEntry2>()
            .WithJunction<SourceEntry>(typeof(IntermediateEntry), typeof(IntermediateEntry2))
            .WithTransform<IntermediateEntry, SinkEntry>()
            .WithTransform<IntermediateEntry2, SinkEntry2>()
            .Build();

        // Act & Assert
        CovenantValidator.Validate(graph);
    }

    [Fact]
    public void ValidateWindowEdgeSucceeds()
    {
        // Arrange: Source -> (window) -> Sink
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithWindow<SourceEntry, SinkEntry>()
            .Build();

        // Act & Assert
        CovenantValidator.Validate(graph);
    }

    [Fact]
    public void ValidateJunctionWithFallbackSucceeds()
    {
        // Arrange: Junction with routes and fallback
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithSink<SinkEntry2>()
            .WithJunctionAndFallback<SourceEntry, SinkEntry2>(typeof(SinkEntry))
            .Build();

        // Act & Assert
        CovenantValidator.Validate(graph);
    }

    #endregion

    #region No Sources

    [Fact]
    public void ValidateNoSourcesThrowsWithMessage()
    {
        // Arrange: Only a sink
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSink<SinkEntry>()
            .Build();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("no sources", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region No Sinks

    [Fact]
    public void ValidateNoSinksThrowsWithMessage()
    {
        // Arrange: Only a source
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .Build();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("no sinks", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Dead Letters

    [Fact]
    public void ValidateDeadLetterThrowsWithTypeName()
    {
        // Arrange: Source produces intermediate, but intermediate is never consumed
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithTransform<SourceEntry, DeadLetterEntry>()
            .Build();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("Dead letter", ex.Message);
        Assert.Contains("DeadLetterEntry", ex.Message);
    }

    [Fact]
    public void ValidateMultipleDeadLettersReportsAll()
    {
        // Arrange: Multiple unconnected paths
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithJunction<SourceEntry>(typeof(IntermediateEntry), typeof(IntermediateEntry2))
            .Build();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("IntermediateEntry", ex.Message);
        Assert.Contains("IntermediateEntry2", ex.Message);
    }

    #endregion

    #region Orphaned Consumers

    [Fact]
    public void ValidateOrphanedConsumerThrowsWithTypeName()
    {
        // Arrange: OrphanEntry -> Sink (but OrphanEntry is never produced)
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithTransform<SourceEntry, IntermediateEntry>()
            .WithTransform<OrphanEntry, SinkEntry>()
            .Build();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("Orphaned consumer", ex.Message);
        Assert.Contains("OrphanEntry", ex.Message);
    }

    #endregion

    #region Islands

    [Fact]
    public void ValidateUnreachableSinkThrowsWithIslandMessage()
    {
        // Arrange: Source -> Sink1, but Sink2 is unreachable
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithSink<SinkEntry2>()
            .WithTransform<SourceEntry, SinkEntry>()
            .Build();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("Island", ex.Message);
        Assert.Contains("SinkEntry2", ex.Message);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateNullGraphThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => CovenantValidator.Validate<TestCovenant>(null!));
    }

    [Fact]
    public void ValidateEmptyGraphThrowsNoSourcesAndSinks()
    {
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create().Build();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CovenantValidator.Validate(graph));
        Assert.Contains("no sources", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no sinks", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSelfLoopSucceeds()
    {
        // Arrange: Entry can transform to itself (via different path to sink)
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithTransform<SourceEntry, IntermediateEntry>()
            .WithTransform<IntermediateEntry, IntermediateEntry>()
            .WithTransform<IntermediateEntry, SinkEntry>()
            .Build();

        // Act & Assert: Cycles are allowed as long as connectivity holds
        CovenantValidator.Validate(graph);
    }

    [Fact]
    public void ValidateConvergingPathsSucceeds()
    {
        // Arrange: Two paths converge to same sink
        CovenantGraph<TestCovenant> graph = TestGraphBuilder.Create()
            .WithSource<SourceEntry>()
            .WithSink<SinkEntry>()
            .WithJunction<SourceEntry>(typeof(IntermediateEntry), typeof(IntermediateEntry2))
            .WithTransform<IntermediateEntry, SinkEntry>()
            .WithTransform<IntermediateEntry2, SinkEntry>()
            .Build();

        CovenantValidator.Validate(graph);
    }

    #endregion
}
