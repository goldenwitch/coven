using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

    private Process? _proc;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private TaskCompletionSource<bool>? _nextMessageRequestedTcs;

    public CodexCliAgent(string codexExecutablePath, string workspaceDirectory, IScrivener<TMessageFormat> scrivener)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
        _scrivener = scrivener;
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
            await using ITailMux mux = new ProcessDocumentTailMux(
                documentPath: Path.Combine(_workspaceDirectory, "codex.log"),
                fileName: _codexExecutablePath,
                workingDirectory: _workspaceDirectory,
                configurePsi: psi =>
                {
                    psi.CreateNoWindow = true;
                });

            await mux.TailAsync(async ev =>
            {
                switch (ev)
                {
                    case Line o:
                        // _ = _scrivener.Thought(o.Line);
                        if (o.Line == "> ")
                        {
                            TMessageFormat msg = await ReadMessage();
                            var payload = msg is string s ? s : msg?.ToString() ?? string.Empty;
                            await mux.WriteLineAsync(payload, ct);
                        }
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

}
