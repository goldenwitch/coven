using System;
using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal static class CodexRolloutParser
{
    public static CodexRolloutLine Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new CodexRolloutLine(CodexRolloutLineKind.Unknown, Raw: line);

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            string? type = TryGetString(root, "type");
            var created = TryGetDateTimeOffset(root, "created");

            switch (type?.ToLowerInvariant())
            {
                case "metadata":
                    return new CodexRolloutLine(
                        CodexRolloutLineKind.Metadata,
                        created,
                        Raw: line,
                        SessionId: TryGetString(root, "session_id") ?? TryGetString(root, "sessionId"));

                case "message":
                    return new CodexRolloutLine(
                        CodexRolloutLineKind.Message,
                        created,
                        Raw: line,
                        Role: TryGetString(root, "role"),
                        Content: TryGetString(root, "content"));

                case "command":
                    return new CodexRolloutLine(
                        CodexRolloutLineKind.Command,
                        created,
                        Raw: line,
                        Command: TryGetString(root, "command") ?? TryGetString(root, "cmd"),
                        Cwd: TryGetString(root, "cwd"));

                case "command_output":
                case "command-output":
                case "cmd_output":
                    return new CodexRolloutLine(
                        CodexRolloutLineKind.CommandOutput,
                        created,
                        Raw: line,
                        Stream: TryGetString(root, "stream"),
                        Text: TryGetString(root, "text") ?? TryGetString(root, "data"));

                case "file_edit":
                case "file-edit":
                case "patch":
                    return new CodexRolloutLine(
                        CodexRolloutLineKind.FileEdit,
                        created,
                        Raw: line,
                        Path: TryGetString(root, "path"),
                        Patch: TryGetString(root, "patch") ?? TryGetString(root, "diff"));

                case "error":
                    return new CodexRolloutLine(
                        CodexRolloutLineKind.Error,
                        created,
                        Raw: line,
                        Message: TryGetString(root, "message"),
                        Code: TryGetString(root, "code"));
            }

            // Unknown type but valid JSON
            return new CodexRolloutLine(CodexRolloutLineKind.Unknown, created, Raw: line);
        }
        catch
        {
            // Not JSON; treat as opaque text
            return new CodexRolloutLine(CodexRolloutLineKind.Unknown, Raw: line);
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
