namespace Coven.Spellcasting.Agents.Codex;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Spells;


public sealed class CodexCliAgent<TOut> : ICovenAgent<string, TOut>
{
    public string Id => "codex";
    private readonly string _codexExecutablePath;
    private readonly string _workspaceDirectory;

    public CodexCliAgent(string codexExecutablePath, string workspaceDirectory)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
    }

    public Task RegisterSpells(List<SpellDefinition> Spells)
    {
        throw new NotImplementedException();
    }

    public async Task<TOut> CastSpell(string input, CancellationToken ct = default)
    {
        throw new NotImplementedException();
        var psi = new ProcessStartInfo
        {
            FileName = _codexExecutablePath,
            WorkingDirectory = _workspaceDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add(input);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        try
        {
            // Read all of what codex says as each line gets posted.

            // Simultaneously validate that no errors stream out.
            // If we get an error, build it into an exception.

            // When Codex posts our "prompt" line, make an Ask call.
        }
        catch (OperationCanceledException)
        {
        }
    }
}
