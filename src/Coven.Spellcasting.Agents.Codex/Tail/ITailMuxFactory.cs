using System.Diagnostics;

namespace Coven.Spellcasting.Agents.Codex.Tail;

public interface ITailMuxFactory
{
    ITailMux CreateForRollout(string rolloutPath, Process? process);
}
