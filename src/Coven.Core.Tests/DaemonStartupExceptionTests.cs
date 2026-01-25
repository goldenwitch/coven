// SPDX-License-Identifier: BUSL-1.1

using Xunit;

namespace Coven.Core.Tests;

public class DaemonStartupExceptionTests
{
    private sealed class TestDaemon1 : IDaemon
    {
        public Status Status => Status.Stopped;
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestDaemon2 : IDaemon
    {
        public Status Status => Status.Stopped;
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void ConstructorSetsAllProperties()
    {
        // Arrange
        InvalidOperationException innerException = new("Daemon failed to connect");
        Type failedDaemon = typeof(TestDaemon2);
        IReadOnlyList<Type> rolledBack = [typeof(TestDaemon1)];

        // Act
        DaemonStartupException exception = new(
            "Daemon startup failed",
            innerException,
            failedDaemon,
            rolledBack);

        // Assert
        Assert.Equal("Daemon startup failed", exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(typeof(TestDaemon2), exception.FailedDaemon);
        Assert.Single(exception.RolledBackDaemons);
        Assert.Equal(typeof(TestDaemon1), exception.RolledBackDaemons[0]);
    }

    [Fact]
    public void RolledBackDaemonsCanBeEmpty()
    {
        // Arrange
        InvalidOperationException innerException = new("First daemon failed");

        // Act
        DaemonStartupException exception = new(
            "First daemon failed to start",
            innerException,
            typeof(TestDaemon1),
            []);

        // Assert
        Assert.Empty(exception.RolledBackDaemons);
        Assert.Equal(typeof(TestDaemon1), exception.FailedDaemon);
    }

    [Fact]
    public void RolledBackDaemonsPreservesOrder()
    {
        // Arrange
        InvalidOperationException innerException = new("Third daemon failed");
        IReadOnlyList<Type> rolledBack = [typeof(TestDaemon2), typeof(TestDaemon1)];

        // Act
        DaemonStartupException exception = new(
            "Startup failed",
            innerException,
            typeof(IDaemon), // hypothetical third daemon
            rolledBack);

        // Assert
        Assert.Equal(2, exception.RolledBackDaemons.Count);
        Assert.Equal(typeof(TestDaemon2), exception.RolledBackDaemons[0]);
        Assert.Equal(typeof(TestDaemon1), exception.RolledBackDaemons[1]);
    }

    [Fact]
    public void ExceptionIsProperlyTyped()
    {
        // DaemonStartupException should be catchable and have proper exception semantics
        InvalidOperationException innerException = new("Connection timeout");
        DaemonStartupException exception = new(
            "Daemon startup failed",
            innerException,
            typeof(TestDaemon1),
            []);

        // Verify it can be caught as Exception
        Assert.IsType<DaemonStartupException>(exception, exactMatch: false);

        // Verify inner exception chain
        Assert.NotNull(exception.InnerException);
        Assert.Equal("Connection timeout", exception.InnerException.Message);
    }
}
