using System.Diagnostics;
using System.IO.Pipes;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents.Validation;
using Coven.Spellcasting.Agents.Codex.Config;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliValidation : IAgentValidation
{
    public string AgentId => "Codex";

    private readonly string _executablePath;
    private readonly string _workspaceDirectory;
    private readonly string? _shimExecutablePath;
    private readonly Spellbook? _spellbook;
    private readonly ICodexConfigWriter _configWriter;

    public CodexCliValidation(
        string executablePath,
        string workspaceDirectory,
        string? shimExecutablePath,
        Spellbook? spellbook,
        ICodexConfigWriter configWriter)
    {
        _executablePath = string.IsNullOrWhiteSpace(executablePath) ? "codex" : executablePath;
        _workspaceDirectory = string.IsNullOrWhiteSpace(workspaceDirectory) ? Directory.GetCurrentDirectory() : workspaceDirectory;
        _shimExecutablePath = shimExecutablePath;
        _spellbook = spellbook;
        _configWriter = configWriter ?? throw new ArgumentNullException(nameof(configWriter));
    }

    public async Task<AgentValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var notes = new List<string>();
        bool performed = false;

        EnsureNotCancelled(ct);
        var versionOk = TryRunProcess(_executablePath, "--version", _workspaceDirectory, null, out string? codexVersionOut);
        if (!versionOk)
            throw new InvalidOperationException("Codex CLI not found or not runnable. Ensure 'codex' is on PATH or set ExecutablePath to an absolute path.");
        if (!string.IsNullOrWhiteSpace(codexVersionOut)) notes.Add($"codex ok: {codexVersionOut.Trim()}"); else notes.Add("codex ok: version probe succeeded");

        EnsureNotCancelled(ct);
        if (!Directory.Exists(_workspaceDirectory)) { Directory.CreateDirectory(_workspaceDirectory); performed = true; notes.Add($"workspace created: {_workspaceDirectory}"); }
        var probePath = Path.Combine(_workspaceDirectory, $".coven_probe_{Guid.NewGuid():N}");
        try { await File.WriteAllTextAsync(probePath, "ok", ct).ConfigureAwait(false); File.Delete(probePath); notes.Add("workspace writable: ok"); }
        catch { throw new InvalidOperationException($"Workspace '{_workspaceDirectory}' is not writable."); }

        EnsureNotCancelled(ct);
        var codexHome = Path.Combine(_workspaceDirectory, ".codex");
        if (!Directory.Exists(codexHome)) { Directory.CreateDirectory(codexHome); performed = true; notes.Add($"codex home created: {codexHome}"); }
        var homeProbe = Path.Combine(codexHome, $"probe_{Guid.NewGuid():N}.tmp");
        try { await File.WriteAllTextAsync(homeProbe, "ok", ct).ConfigureAwait(false); File.Delete(homeProbe); notes.Add("codex home writable: ok"); }
        catch { throw new InvalidOperationException($"Codex home '{codexHome}' is not writable."); }

        bool shimRequired = _spellbook?.Spells?.Count > 0;

        EnsureNotCancelled(ct);
        try
        {
            var pipeName = $"coven_probe_{Guid.NewGuid():N}";
            using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var clientTask = Task.Run(async () =>
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(1000, ct).ConfigureAwait(false);
                await client.WriteAsync(new byte[] { (byte)'P' }, ct).ConfigureAwait(false);
                await client.FlushAsync(ct).ConfigureAwait(false);
            }, ct);
            server.WaitForConnection();
            var buf = new byte[1];
            _ = server.Read(buf, 0, 1);
            notes.Add("named pipes: ok");
        }
        catch { throw new InvalidOperationException("Named pipes are unavailable; MCP shim cannot connect."); }

        EnsureNotCancelled(ct);
        string? shimPath = ResolveShimPath(_shimExecutablePath);
        if (shimRequired)
        {
            if (string.IsNullOrWhiteSpace(shimPath) || !File.Exists(shimPath))
                throw new InvalidOperationException("MCP shim not found. Build output should include mcp-shim/ with the shim executable.");
            var shimOk = TryRunProcess(shimPath, "--help", _workspaceDirectory, null, out _);
            if (!shimOk) throw new InvalidOperationException("MCP shim is not runnable.");
            notes.Add($"shim ok: {shimPath}");
        }

        EnsureNotCancelled(ct);
        try
        {
            var dummyPipe = $"coven_probe_{Guid.NewGuid():N}";
            _configWriter.WriteOrMerge(codexHome, shimPath ?? "shim-not-required", dummyPipe, "coven");
            notes.Add("codex config merge: ok");
        }
        catch { throw new InvalidOperationException("Failed to write or merge Codex config.toml."); }

        EnsureNotCancelled(ct);
        var env = new Dictionary<string, string?> { ["CODEX_HOME"] = codexHome };
        _ = TryRunProcess(_executablePath, "sessions list", _workspaceDirectory, env, out _);
        notes.Add("codex sessions probe: attempted");

        var msg = string.Join("; ", notes);
        return performed ? AgentValidationResult.Performed(msg) : AgentValidationResult.Noop(msg);
    }

    private static void EnsureNotCancelled(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
    }

    private static bool TryRunProcess(
        string fileName,
        string? arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        out string? stdOut)
    {
        stdOut = null;
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments ?? string.Empty)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true
            };
            if (environment is not null)
            {
                foreach (var kv in environment)
                {
                    if (kv.Value is null) psi.Environment.Remove(kv.Key);
                    else psi.Environment[kv.Key] = kv.Value;
                }
            }
            using var p = Process.Start(psi);
            if (p is null) return false;
            stdOut = p.StandardOutput.ReadToEnd();
            p.WaitForExit(4000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string? ResolveShimPath(string? provided)
    {
        if (!string.IsNullOrWhiteSpace(provided) && File.Exists(provided)) return provided;
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var shimDir = Path.Combine(baseDir, "mcp-shim");
            if (Directory.Exists(shimDir))
            {
                var exe = Path.Combine(shimDir, "Coven.Spellcasting.Agents.Codex.McpShim.exe");
                var dll = Path.Combine(shimDir, "Coven.Spellcasting.Agents.Codex.McpShim.dll");
                if (File.Exists(exe)) return exe;
                if (File.Exists(dll)) return dll;
                var any = Directory.GetFiles(shimDir).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(any)) return any;
            }
        }
        catch { }
        return null;
    }
}
