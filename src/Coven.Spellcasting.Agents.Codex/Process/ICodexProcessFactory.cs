using System.Diagnostics;

namespace Coven.Spellcasting.Agents.Codex.Processes;

public interface IProcessHandle : IAsyncDisposable
{
    Process Process { get; }
}

public interface ICodexProcessFactory
{
    IProcessHandle Start(string executablePath, string workingDirectory, IReadOnlyDictionary<string, string?> environment);
}
