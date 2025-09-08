using System;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal sealed class DefaultStringTranslator : ICodexRolloutTranslator<string>
{
    public string Translate(CodexRolloutEvent ev)
    {
        string entry = "";

        switch (ev)
        {
            case MetadataEvent m:
                entry = $"session start: {m.SessionId ?? "?"} @ {m.Created?.ToString("u") ?? "?"}";
                break;

            case CommandEvent c:
                entry = $"$ {c.Command ?? "?"}{(string.IsNullOrWhiteSpace(c.Cwd) ? string.Empty : $" (cwd={c.Cwd})")}";
                break;

            case CommandOutputEvent o:
                // Avoid flooding; prefix stream name if present
                entry = string.IsNullOrEmpty(o.Stream) ? (o.Text ?? string.Empty) : $"[{o.Stream}] {o.Text}";
                break;

            case FileEditEvent fe:
                entry = $"PATCH: {fe.Path ?? "?"}";
                break;

            case ErrorEvent er:
                entry = $"ERROR{(string.IsNullOrWhiteSpace(er.Code) ? string.Empty : $"[{er.Code}]")}: {er.Message}";
                break;

            case MessageEvent me:
                var preview = (me.Content ?? string.Empty);
                if (preview.Length > 160) preview = preview.Substring(0, 160) + "…";
                entry = $"{me.Role ?? "msg"}: {preview}";
                break;

            case UnknownEvent u:
                entry = u.Raw;
                break;
        }

        return entry;
    }
}

