// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;

namespace Coven.Spellcasting.Agents;

public interface ITailMuxFactory
{
    // Generic factory: compose a tail mux from a document path and a process send port.
    // No Codex-specific parameters.
    ITailMux Create(
        string documentPath,
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environment);
}
