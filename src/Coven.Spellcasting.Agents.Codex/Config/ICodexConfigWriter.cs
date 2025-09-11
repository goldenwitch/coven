// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.Config;

public interface ICodexConfigWriter
{
    void WriteOrMerge(string codexHomeDir, string shimPath, string pipeName, string serverKey = "coven");
}