namespace Coven.Spellcasting.Agents.Codex;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class CodexCliAgent<TIn, TOut> : Coven.Spellcasting.Agents.ICovenAgent<TIn, TOut>
{
    public sealed class Options
    {
        public string ExecutablePath { get; init; } = "codex";
        public IReadOnlyList<string> FixedArgs { get; init; } = Array.Empty<string>();
    }

    public string Id => "codex";

    private readonly Func<TIn, string> _toPrompt;
    private readonly Func<string, TOut> _parse;
    private readonly Options _opts;

    public CodexCliAgent(Func<TIn, string> toPrompt, Func<string, TOut> parse, Options? options = null)
    {
        _toPrompt = toPrompt ?? throw new ArgumentNullException(nameof(toPrompt));
        _parse    = parse    ?? throw new ArgumentNullException(nameof(parse));
        _opts     = options  ?? new Options();
    }

    public async Task<TOut> CastSpellAsync(
        TIn input,
        Coven.Spellcasting.Agents.SpellContext? context = null,
        CancellationToken ct = default)
    {
        var prompt = _toPrompt(input);

        var psi = new ProcessStartInfo
        {
            FileName = _opts.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var a in _opts.FixedArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(prompt);

        if (context?.ContextUri is { IsAbsoluteUri: true, Scheme: "file" } uri)
        {
            psi.WorkingDirectory = Path.GetFullPath(uri.LocalPath);
        }

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        var text = stdout.Length > 0 ? stdout : stderr;
        return _parse(text);
    }
}

