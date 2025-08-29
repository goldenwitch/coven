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

        // Map type-safe permissions to a simple autonomy environment variable for the subprocess.
        var perms = context?.Permissions;
        var autonomy = perms?.Allows<Coven.Spellcasting.Agents.RunCommand>() == true ? "full-auto"
                    : perms?.Allows<Coven.Spellcasting.Agents.WriteFile>()  == true ? "auto-edit"
                    : "suggest";
        // Environment requires UseShellExecute=false
        psi.Environment["CODEX_AUTONOMY"] = autonomy;

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        try
        {
            proc.Start();
            var stdOutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stdErrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            var stdout = await stdOutTask.ConfigureAwait(false);
            var stderr = await stdErrTask.ConfigureAwait(false);
            var text = stdout.Length > 0 ? stdout : stderr;
            return _parse(text);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }
    }
}
