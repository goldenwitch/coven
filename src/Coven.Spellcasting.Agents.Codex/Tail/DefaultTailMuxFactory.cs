// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tail;

internal sealed class DefaultTailMuxFactory : ITailMuxFactory
{
    // Generic implementation: simply composes the process/document mux with provided parameters.
    public ITailMux Create(
        string documentPath,
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environment)
    {
        return new ProcessDocumentTailMux(
            documentPath,
            fileName: executablePath,
            arguments: arguments,
            workingDirectory: workingDirectory,
            environment: environment);
    }
}
