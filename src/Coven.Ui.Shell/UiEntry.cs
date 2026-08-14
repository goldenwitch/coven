// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Ui.Shell;

/// <summary>
/// Severity of a <see cref="UiNotice"/>.
/// </summary>
public enum UiNoticeLevel
{
    /// <summary>Routine information, such as a model change.</summary>
    Info = 0,

    /// <summary>Something degraded but recoverable, such as a stale model catalog.</summary>
    Warning = 1,

    /// <summary>A failure the user needs to act on, such as a daemon fault.</summary>
    Error = 2
}

/// <summary>
/// Base entry type for the application shell journal.
/// </summary>
/// <remarks>
/// Kept separate from <c>ChatEntry</c> for two reasons: a branch manifest carries exactly one
/// journal entry type, and application concerns should not appear in the chat transcript.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(UiThought), nameof(UiThought))]
[JsonDerivedType(typeof(UiNotice), nameof(UiNotice))]
public abstract record UiEntry : Entry;

/// <summary>Agent reasoning surfaced for display in a dedicated pane.</summary>
public sealed record UiThought(string Sender, string Text) : UiEntry;

/// <summary>An application-level event worth showing the user.</summary>
/// <param name="Level">Severity of the notice.</param>
/// <param name="Text">Human-readable message.</param>
public sealed record UiNotice(UiNoticeLevel Level, string Text) : UiEntry;
