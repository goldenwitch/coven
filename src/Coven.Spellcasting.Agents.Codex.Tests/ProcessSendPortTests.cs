// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents.Tail;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class ProcessSendPortTests : IDisposable
{
    private readonly List<IAsyncDisposable> _toDispose = new();
    private readonly List<string> _tempFiles = new();

    private ProcessSendPort CreatePort(string exe, IReadOnlyList<string> args, string? ws = null)
    {
        var port = new ProcessSendPort(fileName: exe, arguments: args, workingDirectory: ws);
        _toDispose.Add(port);
        return port;
    }
    private string TrackTempFile(string path) { _tempFiles.Add(path); return path; }
    [Fact]
    public async Task ProcessSendPort_Executes_Command_With_ArgList()
    {
        var exe = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        string[] strings = ["-c", "cat"];
        var args = OperatingSystem.IsWindows()
            ? ["/C", "more"]
            : strings;

        var ws = Path.GetTempPath();

        var port = CreatePort(exe, args, ws);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        // First write lazily starts the process; if the command cannot be executed,
        // this will throw (e.g., FileNotFoundException). Successful completion proves execution.
        await port.WriteLineAsync("hello world", cts.Token);
    }

    [Fact]
    public async Task ProcessSendPort_Passes_Arguments_And_Computes_Sum()
    {
        var outPath = TrackTempFile(Path.Combine(Path.GetTempPath(), $"sum_{Guid.NewGuid():N}.txt"));

        string exe;
        IReadOnlyList<string> args;

        if (OperatingSystem.IsWindows())
        {
            exe = "powershell";
            args = new[]
            {
                "-NoProfile",
                "-Command",
                "& { param($a,$b,$o) Set-Content -Path $o -Value ([int]$a+[int]$b); Start-Sleep -Seconds 2 }",
                "2",
                "3",
                outPath
            };
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            exe = "/bin/sh";
            args = new[] { "-c", $"echo $(( $1 + $2 )) > \"$3\"; sleep 2", "_", "2", "3", outPath };
        }
        else
        {
            throw new NotSupportedException("Unsupported OS for test environment.");
        }

        var port = CreatePort(exe, args);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Trigger lazy start; process stays alive briefly to accept stdin
        await port.WriteLineAsync("start", cts.Token);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!File.Exists(outPath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, cts.Token);
        }
        Assert.True(File.Exists(outPath), "Output file was not created by process");

        var text = await File.ReadAllTextAsync(outPath, cts.Token);
        text = text.Trim();
        Assert.Equal("5", text);
    }

    public void Dispose()
    {
        foreach (var d in _toDispose)
        {
            try { d.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        }
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
        _toDispose.Clear();
        _tempFiles.Clear();
    }
}
