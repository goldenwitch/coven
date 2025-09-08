using System;
using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal static class CodexRolloutParser
{
    public static CodexRolloutEvent Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new UnknownEvent("empty", null, line, null);

        try
        {
            var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            string? type = TryGetString(root, "type");
            var created = TryGetDateTimeOffset(root, "created");

            switch (type?.ToLowerInvariant())
            {
                case "metadata":
                    return new MetadataEvent(
                        SessionId: TryGetString(root, "session_id") ?? TryGetString(root, "sessionId"),
                        Created: created,
                        Raw: line,
                        Document: doc);

                case "message":
                    return new MessageEvent(
                        Role: TryGetString(root, "role"),
                        Content: TryGetString(root, "content"),
                        Timestamp: created,
                        Raw: line,
                        Document: doc);

                case "command":
                    return new CommandEvent(
                        Command: TryGetString(root, "command") ?? TryGetString(root, "cmd"),
                        Cwd: TryGetString(root, "cwd"),
                        Timestamp: created,
                        Raw: line,
                        Document: doc);

                case "command_output":
                case "command-output":
                case "cmd_output":
                    return new CommandOutputEvent(
                        Stream: TryGetString(root, "stream"),
                        Text: TryGetString(root, "text") ?? TryGetString(root, "data"),
                        Timestamp: created,
                        Raw: line,
                        Document: doc);

                case "file_edit":
                case "file-edit":
                case "patch":
                    return new FileEditEvent(
                        Path: TryGetString(root, "path"),
                        Patch: TryGetString(root, "patch") ?? TryGetString(root, "diff"),
                        Timestamp: created,
                        Raw: line,
                        Document: doc);

                case "error":
                    return new ErrorEvent(
                        Message: TryGetString(root, "message"),
                        Code: TryGetString(root, "code"),
                        Timestamp: created,
                        Raw: line,
                        Document: doc);
            }

            // Unknown type but valid JSON
            return new UnknownEvent(type, created, line, doc);
        }
        catch
        {
            // Not JSON; treat as opaque text
            return new UnknownEvent("text", null, line, null);
        }
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.String) return v.GetString();
            return v.ToString();
        }
        return null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(v.GetString(), out var dto)) return dto;
        }
        return null;
    }
}

