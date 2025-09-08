using System;
using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal enum CodexRolloutKind
{
    Metadata,
    Message,
    Command,
    CommandOutput,
    FileEdit,
    Error,
    Unknown
}

internal abstract record CodexRolloutEvent(
    CodexRolloutKind Kind,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
{
    public JsonElement? Data => Document?.RootElement;
}

internal sealed record MetadataEvent(
    string? SessionId,
    DateTimeOffset? Created,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.Metadata, Created, Raw, Document);

internal sealed record MessageEvent(
    string? Role,
    string? Content,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.Message, Timestamp, Raw, Document);

internal sealed record CommandEvent(
    string? Command,
    string? Cwd,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.Command, Timestamp, Raw, Document);

internal sealed record CommandOutputEvent(
    string? Stream,
    string? Text,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.CommandOutput, Timestamp, Raw, Document);

internal sealed record FileEditEvent(
    string? Path,
    string? Patch,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.FileEdit, Timestamp, Raw, Document);

internal sealed record ErrorEvent(
    string? Message,
    string? Code,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.Error, Timestamp, Raw, Document);

internal sealed record UnknownEvent(
    string? ReportedType,
    DateTimeOffset? Timestamp,
    string Raw,
    JsonDocument? Document)
    : CodexRolloutEvent(CodexRolloutKind.Unknown, Timestamp, Raw, Document);

