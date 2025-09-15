// SPDX-License-Identifier: BUSL-1.1

using System.ComponentModel;
using System.Diagnostics;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Tail;

/// <summary>
/// Write-only port that lazily starts a process and writes lines to its stdin.
/// Matches the sending behavior used by ProcessDocumentTailMux.
/// </summary>
    public sealed class ProcessSendPort : ISendPort, IAsyncDisposable
    {
    private readonly string _fileName;
    private readonly IReadOnlyList<string>? _arguments;
    private readonly string? _workingDirectory;
    private readonly IReadOnlyDictionary<string, string?>? _environment;
    private readonly Action<ProcessStartInfo>? _configurePsi;

    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Process? _proc;
    private volatile bool _started;
    private volatile bool _disposed;

    public ProcessSendPort(
        string fileName,
        IReadOnlyList<string>? arguments = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<ProcessStartInfo>? configurePsi = null)
    {
        _fileName = fileName;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
        _environment = environment;
        _configurePsi = configurePsi;
    }

        public async Task WriteAsync(string data, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var p = _proc!;
                if (p.HasExited) throw new InvalidOperationException("Process has already exited.");
                if (p.StandardInput is null) throw new InvalidOperationException("Process stdin is not available.");

                await p.StandardInput.WriteAsync(data).WaitAsync(ct).ConfigureAwait(false);
                await p.StandardInput.FlushAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

    private async Task EnsureStartedAsync(CancellationToken ct)
    {
        if (_started) return;
        await _startGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_started) return;
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessSendPort));

            if (!string.IsNullOrWhiteSpace(_workingDirectory) && !Directory.Exists(_workingDirectory))
            {
                throw new DirectoryNotFoundException($"Workspace directory not found: {_workingDirectory}");
            }

            var psi = new ProcessStartInfo(_fileName)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            if (_arguments is not null)
            {
                foreach (var arg in _arguments)
                {
                    psi.ArgumentList.Add(arg);
                }
            }

            if (!string.IsNullOrWhiteSpace(_workingDirectory))
                psi.WorkingDirectory = _workingDirectory!;

            if (_environment is not null)
            {
                foreach (var kvp in _environment)
                {
                    if (kvp.Value is null) psi.Environment.Remove(kvp.Key);
                    else psi.Environment[kvp.Key] = kvp.Value;
                }
            }

            _configurePsi?.Invoke(psi);

            _proc = new Process { StartInfo = psi };
            try
            {
                if (!_proc.Start())
                    throw new InvalidOperationException("Failed to start process.");
            }
            catch (Win32Exception win32Ex)
            {
                // Common case: executable not found or not executable on PATH
                throw new FileNotFoundException($"Executable not found or not executable: '{_fileName}'. Ensure it is on PATH or specify an absolute path via configuration.", _fileName, win32Ex);
            }
            catch (DirectoryNotFoundException)
            {
                // Propagate as-is with clear message set above
                throw;
            }

            _started = true;
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // First, try to close stdin to allow a graceful exit if the child honors EOF
        try { _proc?.StandardInput?.Close(); } catch { }
        // Then ensure the process is not left running
        try { _proc?.Kill(entireProcessTree: true); } catch { }
        try { if (_proc is not null) await _proc.WaitForExitAsync().ConfigureAwait(false); } catch { }
        _proc?.Dispose();
        _writeLock.Dispose();
        _startGate.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProcessSendPort));
    }
}
