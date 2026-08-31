// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Daemonology;
using Xunit;

namespace Coven.Core.Tests;

/// <summary>
/// Tests for <see cref="DaemonPumps.WhenAllOrFirstFault"/>, the primitive every leaf session
/// uses to supervise its pumps.
/// </summary>
/// <remarks>
/// The behaviour under test is precisely what <see cref="Task.WhenAll(Task[])"/> does not do:
/// surface a fault while sibling tasks are still running. Journal-tailing pumps only end on
/// cancellation, so a session built on <c>WhenAll</c> never observes a gateway fault, its
/// daemon never fails, and the caller waits forever on a turn that is already dead.
/// </remarks>
public class DaemonPumpsTests
{
    /// <summary>
    /// The core guarantee: a fault surfaces even though the other pump never completes.
    /// Under <see cref="Task.WhenAll(Task[])"/> this would hang until the timeout.
    /// </summary>
    [Fact]
    public async Task FaultSurfacesWhileOtherPumpsStillRun()
    {
        using CancellationTokenSource neverCompletes = new();

        Task faulting = Task.FromException(new InvalidOperationException("gateway exploded"));
        Task endless = Task.Delay(Timeout.Infinite, neverCompletes.Token);

        Task supervised = DaemonPumps.WhenAllOrFirstFault(faulting, endless);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => supervised.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal("gateway exploded", error.Message);

        await neverCompletes.CancelAsync();
    }

    /// <summary>A fault that arrives later is surfaced just as promptly.</summary>
    [Fact]
    public async Task LateFaultSurfaces()
    {
        using CancellationTokenSource neverCompletes = new();

        TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task endless = Task.Delay(Timeout.Infinite, neverCompletes.Token);

        Task supervised = DaemonPumps.WhenAllOrFirstFault(pending.Task, endless);

        Assert.False(supervised.IsCompleted);

        pending.SetException(new HttpRequestException("credit balance too low"));

        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(
            () => supervised.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal("credit balance too low", error.Message);

        await neverCompletes.CancelAsync();
    }

    /// <summary>Normal completion still waits for every pump, matching Task.WhenAll.</summary>
    [Fact]
    public async Task CompletesOnlyAfterEveryPumpCompletes()
    {
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task supervised = DaemonPumps.WhenAllOrFirstFault(first.Task, second.Task);

        first.SetResult();
        Assert.False(supervised.IsCompleted);

        second.SetResult();
        await supervised.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(supervised.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Cancellation surfaces as cancellation, so a daemon's cooperative-shutdown filter
    /// keeps working and a normal stop is not reported as a failure.
    /// </summary>
    [Fact]
    public async Task CancellationSurfacesAsCancellation()
    {
        using CancellationTokenSource cts = new();

        Task cancelling = Task.Delay(Timeout.Infinite, cts.Token);
        TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task supervised = DaemonPumps.WhenAllOrFirstFault(cancelling, pending.Task);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => supervised.WaitAsync(TimeSpan.FromSeconds(5)));

        pending.SetResult();
    }

    /// <summary>No pumps is a no-op rather than a hang.</summary>
    [Fact]
    public async Task EmptySetCompletesImmediately()
    {
        await DaemonPumps.WhenAllOrFirstFault().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
