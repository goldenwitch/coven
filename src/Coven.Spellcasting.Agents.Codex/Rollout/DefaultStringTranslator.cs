using System;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal sealed class DefaultStringTranslator : ICodexRolloutTranslator<string>
{
    public string Translate(CodexRolloutLine line)
    {
        string entry = "";

        switch (line.Kind)
        {
            case CodexRolloutLineKind.Metadata:
                entry = $"session start: {line.SessionId ?? "?"} @ {line.Timestamp?.ToString("u") ?? "?"}";
                break;

            case CodexRolloutLineKind.Command:
                entry = $"$ {line.Command ?? "?"}{(string.IsNullOrWhiteSpace(line.Cwd) ? string.Empty : $" (cwd={line.Cwd})")}";
                break;

            case CodexRolloutLineKind.CommandOutput:
                // Avoid flooding; prefix stream name if present
                entry = string.IsNullOrEmpty(line.Stream) ? (line.Text ?? string.Empty) : $"[{line.Stream}] {line.Text}";
                break;

            case CodexRolloutLineKind.FileEdit:
                entry = $"PATCH: {line.Path ?? "?"}";
                break;

            case CodexRolloutLineKind.Error:
                entry = $"ERROR{(string.IsNullOrWhiteSpace(line.Code) ? string.Empty : $"[{line.Code}]")}: {line.Message}";
                break;

            case CodexRolloutLineKind.Message:
                var preview = (line.Content ?? string.Empty);
                if (preview.Length > 160) preview = preview.Substring(0, 160) + "…";
                entry = $"{line.Role ?? "msg"}: {preview}";
                break;

            case CodexRolloutLineKind.Unknown:
                entry = line.Raw ?? string.Empty;
                break;
        }

        return entry;
    }
}
