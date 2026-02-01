// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class InnerCovenantBuilderTests
{
    [Fact]
    public void ValidInnerCovenantBuildsSuccessfully()
    {
        // Arrange
        HashSet<Type> boundaryProduces = [typeof(BoundaryOutput), typeof(BoundaryFault)];
        HashSet<Type> boundaryConsumes = [typeof(BoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(BoundaryEntry), boundaryProduces, boundaryConsumes);

        BranchManifest innerA = builder.Branch(
            "InnerA",
            typeof(InnerAEntry),
            produces: new HashSet<Type> { typeof(InnerAOutput) },
            consumes: new HashSet<Type> { typeof(InnerAInput) },
            daemons: []);

        builder.Connect(innerA);

        // Act
        builder.Routes(c =>
        {
            c.Route<BoundaryInput, InnerAInput>((_, _) => Task.FromResult(new InnerAInput()));
            c.Route<InnerAOutput, BoundaryOutput>((_, _) => Task.FromResult(new BoundaryOutput()));
            c.Terminal<BoundaryFault>();
        });

        // Assert
        Assert.NotEmpty(builder.InnerPumps);
        Assert.Equal(2, builder.InnerPumps.Count);
    }

    [Fact]
    public void MissingRouteForBoundaryProducesThrowsValidationException()
    {
        // Arrange
        HashSet<Type> boundaryProduces = [typeof(BoundaryOutput), typeof(BoundaryFault)];
        HashSet<Type> boundaryConsumes = [typeof(BoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(BoundaryEntry), boundaryProduces, boundaryConsumes);

        BranchManifest innerA = builder.Branch(
            "InnerA",
            typeof(InnerAEntry),
            produces: new HashSet<Type> { typeof(InnerAOutput) },
            consumes: new HashSet<Type> { typeof(InnerAInput) },
            daemons: []);

        builder.Connect(innerA);

        // Act & Assert
        CovenantValidationException ex = Assert.Throws<CovenantValidationException>(() =>
        {
            builder.Routes(c =>
            {
                c.Route<BoundaryInput, InnerAInput>((_, _) => Task.FromResult(new InnerAInput()));
                c.Route<InnerAOutput, BoundaryOutput>((_, _) => Task.FromResult(new BoundaryOutput()));
                // Missing route or terminal for BoundaryFault
            });
        });

        Assert.Contains("BoundaryFault", ex.Message);
    }

    [Fact]
    public void MissingRouteForBoundaryConsumesThrowsValidationException()
    {
        // Arrange
        HashSet<Type> boundaryProduces = [typeof(BoundaryOutput)];
        HashSet<Type> boundaryConsumes = [typeof(BoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(BoundaryEntry), boundaryProduces, boundaryConsumes);

        BranchManifest innerA = builder.Branch(
            "InnerA",
            typeof(InnerAEntry),
            produces: new HashSet<Type> { typeof(InnerAOutput) },
            consumes: new HashSet<Type> { typeof(InnerAInput) },
            daemons: []);

        builder.Connect(innerA);

        // Act & Assert
        CovenantValidationException ex = Assert.Throws<CovenantValidationException>(() =>
        {
            builder.Routes(c =>
            {
                // Missing route FROM BoundaryInput
                c.Route<InnerAOutput, BoundaryOutput>((_, _) => Task.FromResult(new BoundaryOutput()));
                c.Terminal<InnerAInput>();
            });
        });

        Assert.Contains("BoundaryInput", ex.Message);
    }

    [Fact]
    public void DuplicateRouteSourceThrowsValidationException()
    {
        // Arrange
        HashSet<Type> boundaryProduces = [typeof(BoundaryOutput)];
        HashSet<Type> boundaryConsumes = [typeof(BoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(BoundaryEntry), boundaryProduces, boundaryConsumes);

        BranchManifest innerA = builder.Branch(
            "InnerA",
            typeof(InnerAEntry),
            produces: new HashSet<Type> { typeof(InnerAOutput) },
            consumes: new HashSet<Type> { typeof(InnerAInput) },
            daemons: []);

        builder.Connect(innerA);

        // Act & Assert
        CovenantValidationException ex = Assert.Throws<CovenantValidationException>(() =>
        {
            builder.Routes(c =>
            {
                // Two routes from the same source type
                c.Route<BoundaryInput, InnerAInput>((_, _) => Task.FromResult(new InnerAInput()));
                c.Route<BoundaryInput, BoundaryOutput>((_, _) => Task.FromResult(new BoundaryOutput()));
                c.Route<InnerAOutput, BoundaryOutput>((_, _) => Task.FromResult(new BoundaryOutput()));
            });
        });

        Assert.Contains("multiple routes", ex.Message);
    }

    [Fact]
    public void InnerProducesWithoutRouteThrowsValidationException()
    {
        // Arrange
        HashSet<Type> boundaryProduces = [typeof(BoundaryOutput)];
        HashSet<Type> boundaryConsumes = [typeof(BoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(BoundaryEntry), boundaryProduces, boundaryConsumes);

        BranchManifest innerA = builder.Branch(
            "InnerA",
            typeof(InnerAEntry),
            produces: new HashSet<Type> { typeof(InnerAOutput) },
            consumes: new HashSet<Type> { typeof(InnerAInput) },
            daemons: []);

        builder.Connect(innerA);

        // Act & Assert
        CovenantValidationException ex = Assert.Throws<CovenantValidationException>(() =>
        {
            builder.Routes(c =>
            {
                c.Route<BoundaryInput, InnerAInput>((_, _) => Task.FromResult(new InnerAInput()));
                // Missing route from InnerAOutput - it's produced but not routed anywhere
                c.Terminal<BoundaryOutput>();
            });
        });

        Assert.Contains("InnerAOutput", ex.Message);
    }

    [Fact]
    public void TerminalTypeAllowsNoRouteFromIt()
    {
        // Arrange
        HashSet<Type> boundaryProduces = [typeof(BoundaryOutput)];
        HashSet<Type> boundaryConsumes = [typeof(BoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(BoundaryEntry), boundaryProduces, boundaryConsumes);

        BranchManifest innerA = builder.Branch(
            "InnerA",
            typeof(InnerAEntry),
            produces: new HashSet<Type> { typeof(InnerAOutput) },
            consumes: new HashSet<Type> { typeof(InnerAInput) },
            daemons: []);

        builder.Connect(innerA);

        // Act - should not throw because InnerAOutput is marked as terminal
        builder.Routes(c =>
        {
            c.Route<BoundaryInput, InnerAInput>((_, _) => Task.FromResult(new InnerAInput()));
            c.Route<InnerAInput, BoundaryOutput>((_, _) => Task.FromResult(new BoundaryOutput()));
            c.Terminal<InnerAOutput>();
        });

        // Assert
        Assert.Single(builder.InnerPumps, p => p.SourceType == typeof(BoundaryInput));
    }
}
