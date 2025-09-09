using Coven.Chat;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

// Public default translator that maps Codex rollout lines to ChatEntry messages for adapters.
public sealed class DefaultChatEntryTranslator : ICodexRolloutTranslator<ChatEntry>
{
    public ChatEntry Translate(CodexRolloutLine line)
    {
        // By default, emit ChatResponse entries from the "codex" sender.
        string sender = "codex";
        string text = string.Empty;

        switch (line.Kind)
        {
            case CodexRolloutLineKind.Metadata:
                text = $"session start: {line.SessionId ?? "?"} @ {line.Timestamp?.ToString("u") ?? "?"}";
                break;

            case CodexRolloutLineKind.Command:
                text = $"$ {line.Command ?? "?"}{(string.IsNullOrWhiteSpace(line.Cwd) ? string.Empty : $" (cwd={line.Cwd})")}";
                break;

            case CodexRolloutLineKind.CommandOutput:
                text = string.IsNullOrEmpty(line.Stream) ? (line.Text ?? string.Empty) : $"[{line.Stream}] {line.Text}";
                break;

            case CodexRolloutLineKind.FileEdit:
                text = $"PATCH: {line.Path ?? "?"}";
                break;

            case CodexRolloutLineKind.Error:
                text = $"ERROR{(string.IsNullOrWhiteSpace(line.Code) ? string.Empty : $"[{line.Code}]")}: {line.Message}";
                break;

            case CodexRolloutLineKind.Message:
                text = (line.Content ?? string.Empty).TrimEnd();
                break;

            case CodexRolloutLineKind.Unknown:
                text = line.Raw ?? string.Empty;
                break;
        }

        return new ChatResponse(sender, text);
    }
}

