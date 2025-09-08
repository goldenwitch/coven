using Coven.Spellcasting.Agents.Codex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class ProcessDocumentTailMuxFixture : ITailMuxFixture, IDisposable
{
    private readonly IHost _host;
    private readonly Dictionary<ITestTailMux, string> _paths = new();

    public ProcessDocumentTailMuxFixture()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddTransient<Func<string, ITailMux>>(sp => docPath =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        return new ProcessDocumentTailMux(
                            documentPath: docPath,
                            fileName: "cmd.exe",
                            arguments: "/C more",
                            workingDirectory: Path.GetTempPath());
                    }
                    else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        return new ProcessDocumentTailMux(
                            documentPath: docPath,
                            fileName: "/bin/sh",
                            arguments: "-c cat",
                            workingDirectory: Path.GetTempPath());
                    }
                    throw new NotSupportedException("Unsupported OS for test environment.");
                });
            })
            .Build();
    }

    public ITestTailMux CreateMux()
    {
        string path = Path.Combine(Path.GetTempPath(), $"coven_mux_{Guid.NewGuid():N}.log");
        // Intentionally do not create the file here; allows tests to assert wait-for-existence behavior.
        var factory = _host.Services.GetRequiredService<Func<string, ITailMux>>();
        var adapter = new MuxAdapter(factory(path));
        _paths[adapter] = path;
        return adapter;
    }

    public async Task StimulateIncomingAsync(ITestTailMux mux, IEnumerable<string> lines)
    {
        if (!_paths.TryGetValue(mux, out var path)) throw new InvalidOperationException("Unknown mux instance");
        // Ensure file exists before appending. If created now, give the tailer a brief window to
        // detect and open the file before we append, to avoid losing lines due to initial seek-to-end.
        bool createdNow = false;
        if (!File.Exists(path)) { using (File.Create(path)) { } createdNow = true; }
        // No explicit readiness await here; tests can send a sentinel and wait for it.
        await TailMuxTestHelpers.AppendLinesAsync(path, lines);
    }

    public Task CreateBackingFileAsync(ITestTailMux mux)
    {
        if (!_paths.TryGetValue(mux, out var path)) throw new InvalidOperationException("Unknown mux instance");
        if (!File.Exists(path)) using (File.Create(path)) { }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
