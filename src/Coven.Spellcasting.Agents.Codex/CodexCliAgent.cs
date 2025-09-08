using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Coven.Chat;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliAgent<TMessageFormat> : ICovenAgent<TMessageFormat> where TMessageFormat : notnull
{
    public string Id => "codex";
    private readonly string _codexExecutablePath;
    private readonly string _workspaceDirectory;
    private readonly List<SpellDefinition> _registeredSpells = new();
    private readonly IScrivener<TMessageFormat> _scrivener;
    private readonly string _codexHomeDir;

    // Removed unused process/task tracking fields from an earlier design.

    public CodexCliAgent(string codexExecutablePath, string workspaceDirectory, IScrivener<TMessageFormat> scrivener)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
        _scrivener = scrivener;
        _codexHomeDir = Path.Combine(_workspaceDirectory, ".codex");
        try { Directory.CreateDirectory(_codexHomeDir); } catch { }
    }

    public Task RegisterSpells(List<SpellDefinition> Spells)
    {
        _registeredSpells.Clear();
        _registeredSpells.AddRange(Spells ?? new List<SpellDefinition>());

        // Build MCP tools for each spell.

        return Task.CompletedTask;
    }


    public async Task InvokeAgent(CancellationToken ct = default)
    {
        try
        {
            var env = new Dictionary<string, string?>
            {
                ["CODEX_HOME"] = _codexHomeDir
            };

            var documentPath = Path.Combine(_workspaceDirectory, "codex.log");

            await using ITailMux mux = new ProcessDocumentTailMux(
                documentPath: documentPath,
                fileName: _codexExecutablePath,
                arguments: null,
                workingDirectory: _workspaceDirectory,
                environment: env,
                configurePsi: psi =>
                {
                    psi.CreateNoWindow = true;
                });

            await mux.TailAsync(async ev =>
            {
                switch (ev)
                {
                    case Line o:
                        break;

                    case ErrorLine e:
                        // _ = _scrivener.Error(e.Line);
                        break;
                }
            }, ct);
        }
        catch
        {

        }
    }

    public Task CloseAgent()
    {
        throw new NotImplementedException();
    }

    public Task<TMessageFormat> ReadMessage()
    {
        throw new NotImplementedException();
    }

    public Task SendMessage(TMessageFormat message)
    {
        throw new NotImplementedException();
    }

    // helpers removed; document path passed directly for now

}
