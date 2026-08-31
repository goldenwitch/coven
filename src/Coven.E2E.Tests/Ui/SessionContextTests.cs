// SPDX-License-Identifier: BUSL-1.1

using Coven.Ui.Desktop;
using Xunit;

namespace Coven.E2E.Tests.Ui;

/// <summary>
/// The hand-off between a ritual scope and the interface. The session starts on a background
/// task before the view model exists, so everything here is about what a late subscriber sees.
/// </summary>
public sealed class SessionContextTests
{
    /// <summary>
    /// A failure reported before anyone subscribed is replayed on subscription. Without this
    /// a fast startup failure is delivered to an empty handler list and disappears, leaving a
    /// window that looks idle rather than broken.
    /// </summary>
    [Fact]
    public void FailureReportedBeforeSubscribingIsReplayed()
    {
        SessionContext context = new();
        InvalidOperationException error = new("model would not load");

        context.Fail(error);

        Exception? seen = null;
        context.SubscribeToFailure(e => seen = e);

        Assert.Same(error, seen);
    }

    /// <summary>A subscriber already in place is still notified directly.</summary>
    [Fact]
    public void FailureReachesAnExistingSubscriber()
    {
        SessionContext context = new();
        Exception? seen = null;
        context.SubscribeToFailure(e => seen = e);

        InvalidOperationException error = new("gateway rejected the key");
        context.Fail(error);

        Assert.Same(error, seen);
    }

    /// <summary>
    /// The first failure is the one retained. Later faults are usually consequences of it,
    /// and the original is what explains the session.
    /// </summary>
    [Fact]
    public void TheFirstFailureIsTheOneReplayed()
    {
        SessionContext context = new();
        InvalidOperationException first = new("first");

        context.Fail(first);
        context.Fail(new InvalidOperationException("second"));

        Exception? seen = null;
        context.SubscribeToFailure(e => seen = e);

        Assert.Same(first, seen);
    }

    /// <summary>
    /// A rebuild clears the failure, so the next session does not inherit the last one's
    /// error the moment its view model subscribes.
    /// </summary>
    [Fact]
    public void ClearingDropsTheRetainedFailure()
    {
        SessionContext context = new();
        context.Fail(new InvalidOperationException("previous session"));

        context.Clear();

        Exception? seen = null;
        context.SubscribeToFailure(e => seen = e);

        Assert.Null(seen);
    }

    /// <summary>
    /// Notices are written through the journal published from inside the ritual scope, so the
    /// context has to hand back exactly the instance it was given.
    /// </summary>
    [Fact]
    public void JournalIsNullUntilTheRitualPublishesOne()
    {
        SessionContext context = new();

        Assert.Null(context.Journal);
    }
}
