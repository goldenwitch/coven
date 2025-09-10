using System;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

public enum CodexRolloutLineKind
{
    Metadata,
    Message,
    Command,
    CommandOutput,
    FileEdit,
    Error,
    Unknown
}

public sealed record CodexRolloutLine(
    CodexRolloutLineKind Kind,
    DateTimeOffset? Timestamp = null,
    string? Raw = null,
    string? Role = null,
    string? Content = null,
    string? Command = null,
    string? Cwd = null,
    string? Stream = null,
    string? Text = null,
    string? Path = null,
    string? Patch = null,
    string? Message = null,
    string? Code = null,
    string? SessionId = null);

// Previously: internal event types were converted to this public type.
// Now: parser emits CodexRolloutLine directly; no converter required.
