// SPDX-License-Identifier: BUSL-1.1

using Coven.Ui.Desktop.ViewModels;
using Xunit;

namespace Coven.E2E.Tests.Ui;

/// <summary>
/// The per-turn ordering boundary. Streaming fragments and the finalized response travel as
/// two covenant routes with independent cursors, so a fragment can reach the interface after
/// the response it belongs to; these pin what happens when it does.
/// </summary>
public sealed class StreamingTurnTests
{
    /// <summary>Fragments accumulate and drain in order while the turn is open.</summary>
    [Fact]
    public void OpenTurnAccumulatesFragments()
    {
        StreamingTurn turn = new();
        turn.Open();

        Assert.True(turn.Append("Hello"));
        // The second fragment joins the pending text but needs no second drain scheduled.
        Assert.False(turn.Append(", world"));

        Assert.Equal("Hello, world", turn.Drain());
        Assert.Equal(string.Empty, turn.Drain());
    }

    /// <summary>
    /// The case the boundary exists for: a fragment that arrives after its own response is
    /// refused, rather than opening a second streaming message beneath the finished one.
    /// </summary>
    [Fact]
    public void FragmentArrivingAfterTheResponseIsRefused()
    {
        StreamingTurn turn = new();
        turn.Open();

        turn.Append("partial ");
        Assert.Equal("partial ", turn.Drain());

        // The finalized response lands.
        turn.Close();

        // A fragment that overtook it now shows up.
        Assert.False(turn.Append("late fragment"));
        Assert.Equal(string.Empty, turn.Drain());
        Assert.True(turn.IsClosed);
    }

    /// <summary>
    /// A fragment that slips in between the drain and the close is dropped too. Both run on
    /// the interface thread, but a fragment arrives on a journal pump and can land between.
    /// </summary>
    [Fact]
    public void FragmentBetweenDrainAndCloseIsDropped()
    {
        StreamingTurn turn = new();
        turn.Open();
        turn.Append("first");

        Assert.Equal("first", turn.Drain());
        turn.Append("raced in");
        turn.Close();

        Assert.Equal(string.Empty, turn.Drain());
    }

    /// <summary>Opening the next turn clears the boundary and any stale text with it.</summary>
    [Fact]
    public void OpeningTheNextTurnReleasesTheBoundary()
    {
        StreamingTurn turn = new();
        turn.Open();
        turn.Append("first turn");
        turn.Close();

        turn.Open();

        Assert.False(turn.IsClosed);
        Assert.True(turn.Append("second turn"));
        Assert.Equal("second turn", turn.Drain());
    }

    /// <summary>
    /// A turn that never finalized — a failed or abandoned response — does not leak its text
    /// into the next one.
    /// </summary>
    [Fact]
    public void AbandonedTurnDoesNotLeakIntoTheNext()
    {
        StreamingTurn turn = new();
        turn.Open();
        turn.Append("abandoned");

        turn.Open();

        Assert.Equal(string.Empty, turn.Drain());
    }
}
