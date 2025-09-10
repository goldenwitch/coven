using System.Diagnostics;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tail;

internal sealed class DefaultTailMuxFactory : ITailMuxFactory
{
    public ITailMux CreateForRollout(string rolloutPath, Process? process)
    {
        if (process is null)
        {
            // Fallback to tail-only: write-only path will be unused
            return new ProcessDocumentTailMux(rolloutPath, fileName: "cmd", arguments: null);
        }
        return new ProcessDocumentTailMux(rolloutPath, process);
    }
}
