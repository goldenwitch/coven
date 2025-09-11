// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

/// <summary>
/// Fixture that provides a process-backed tail mux which tails a file and writes to a child process.
/// Responsibilities:
/// - Creates a unique temp file path per mux instance but does not create the file until requested.
/// - Builds a platform-appropriate child process that echoes stdin to stdout (Windows: cmd /C more, *nix: sh -c cat).
/// - Stimulates incoming data by appending to the managed file path; ensures the file exists on first append.
/// Notes:
/// - Readiness is managed by tests using a sentinel line; this fixture does not block or poll for readiness.
/// </summary>
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

    /// <summary>
    /// Create a new process-backed mux bound to a fresh temp file path (not created yet).
    /// Tracks the path internally for subsequent file creation and appends.
    /// </summary>
    public ITestTailMux CreateMux()
    {
        string path = Path.Combine(Path.GetTempPath(), $"coven_mux_{Guid.NewGuid():N}.log");
        // Intentionally do not create the file here; allows tests to assert wait-for-existence behavior.
        var factory = _host.Services.GetRequiredService<Func<string, ITailMux>>();
        var adapter = new MuxAdapter(factory(path));
        _paths[adapter] = path;
        return adapter;
    }

    /// <summary>
    /// Append lines to the managed file, creating it if needed.
    /// </summary>
    public async Task StimulateIncomingAsync(ITestTailMux mux, IEnumerable<string> lines)
    {
        if (!_paths.TryGetValue(mux, out var path)) throw new InvalidOperationException("Unknown mux instance");
        // Ensure file exists before appending. If created now, give the tailer a brief window to
        // detect and open the file before we append, to avoid losing lines due to initial seek-to-end.
        if (!File.Exists(path)) { using (File.Create(path)) { } }
        // No explicit readiness await here; tests can send a sentinel and wait for it.
        await TailMuxTestHelpers.AppendLinesAsync(path, lines);
    }

    /// <summary>
    /// Create the backing file for the mux if it does not exist. Tests call this when they are ready to begin tailing.
    /// </summary>
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