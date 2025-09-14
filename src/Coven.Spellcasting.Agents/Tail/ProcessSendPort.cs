// SPDX-License-Identifier: BUSL-1.1

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
    private readonly string? _arguments;
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
        string? arguments = null,
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

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureStartedAsync(ct).ConfigureAwait(false);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var p = _proc!;
            if (p.HasExited) throw new InvalidOperationException("Process has already exited.");
            if (p.StandardInput is null) throw new InvalidOperationException("Process stdin is not available.");

            await p.StandardInput.WriteLineAsync(line).WaitAsync(ct).ConfigureAwait(false);
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

            var psi = new ProcessStartInfo(_fileName, _arguments ?? string.Empty)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

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
            if (!_proc.Start())
                throw new InvalidOperationException("Failed to start process.");

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
