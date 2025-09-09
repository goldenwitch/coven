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

// Internal bridge: convert internal rollout events to public CodexRolloutLine
internal static class CodexRolloutEventConverter
{
    public static CodexRolloutLine ToPublic(CodexRolloutEvent ev) => ev switch
    {
        MetadataEvent m => new CodexRolloutLine(
            CodexRolloutLineKind.Metadata,
            m.Timestamp,
            m.Raw,
            SessionId: m.SessionId),

        MessageEvent me => new CodexRolloutLine(
            CodexRolloutLineKind.Message,
            me.Timestamp,
            me.Raw,
            Role: me.Role,
            Content: me.Content),

        CommandEvent ce => new CodexRolloutLine(
            CodexRolloutLineKind.Command,
            ce.Timestamp,
            ce.Raw,
            Command: ce.Command,
            Cwd: ce.Cwd),

        CommandOutputEvent co => new CodexRolloutLine(
            CodexRolloutLineKind.CommandOutput,
            co.Timestamp,
            co.Raw,
            Stream: co.Stream,
            Text: co.Text),

        FileEditEvent fe => new CodexRolloutLine(
            CodexRolloutLineKind.FileEdit,
            fe.Timestamp,
            fe.Raw,
            Path: fe.Path,
            Patch: fe.Patch),

        ErrorEvent er => new CodexRolloutLine(
            CodexRolloutLineKind.Error,
            er.Timestamp,
            er.Raw,
            Message: er.Message,
            Code: er.Code),

        UnknownEvent u => new CodexRolloutLine(
            CodexRolloutLineKind.Unknown,
            u.Timestamp,
            u.Raw),

        _ => new CodexRolloutLine(CodexRolloutLineKind.Unknown, Raw: ev.Raw)
    };
}

