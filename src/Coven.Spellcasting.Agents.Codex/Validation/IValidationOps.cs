using System.Diagnostics;
using System.IO.Pipes;
using Coven.Spellcasting.Agents.Codex.Config;

namespace Coven.Spellcasting.Agents.Codex.Validation;

internal sealed record ProcessRunResult(bool Ok, string? StdOut);

internal interface IValidationOps
{
    ProcessRunResult RunProcess(string fileName, string? arguments, string workingDirectory, IReadOnlyDictionary<string, string?>? environment);
    bool FileExists(string path);
    void EnsureDirectory(string path);
    Task WriteFileAsync(string path, string contents, CancellationToken ct);
    void DeleteFile(string path);
    void PipeHandshake(string pipeName, CancellationToken ct);
    void MergeConfig(ICodexConfigWriter writer, string codexHomeDir, string shimPath, string pipeName, string serverKey);
}

internal sealed class DefaultValidationOps : IValidationOps
{
    public ProcessRunResult RunProcess(string fileName, string? arguments, string workingDirectory, IReadOnlyDictionary<string, string?>? environment)
    {
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
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv)) psi.Environment["PATH"] = pathEnv;
            using var p = Process.Start(psi);
            if (p is null) return new ProcessRunResult(false, null);
            var stdOut = p.StandardOutput.ReadToEnd();
            p.WaitForExit(4000);
            return new ProcessRunResult(p.ExitCode == 0, stdOut);
        }
        catch { return new ProcessRunResult(false, null); }
    }

    public bool FileExists(string path) => File.Exists(path);

    public void EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public Task WriteFileAsync(string path, string contents, CancellationToken ct)
        => File.WriteAllTextAsync(path, contents, ct);

    public void DeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void PipeHandshake(string pipeName, CancellationToken ct)
    {
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
    }

    public void MergeConfig(ICodexConfigWriter writer, string codexHomeDir, string shimPath, string pipeName, string serverKey)
    {
        writer.WriteOrMerge(codexHomeDir, shimPath, pipeName, serverKey);
    }
}
