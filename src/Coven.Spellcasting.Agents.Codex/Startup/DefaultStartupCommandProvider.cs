// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.Startup;

internal sealed class DefaultStartupCommandProvider : ICodexStartupCommandProvider
{
    private readonly string _startUpCommand;
    public DefaultStartupCommandProvider(string startUpCommand = "")
    {
        _startUpCommand = startUpCommand;
    }

    public string Build()
    {
        return string.IsNullOrWhiteSpace(_startUpCommand) ? "codex" : _startUpCommand;
    }
}
