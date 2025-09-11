// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Diagnostics;
using System.Threading.Channels;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex;

/// <summary>
/// Asymmetric tail multiplexer that reads from a specified document (file) as the input stream
/// and writes to a lazily-started child process for output. Reading and writing paths are decoupled.
/// </summary>
internal sealed class ProcessDocumentTailMux : ITailMux
{
    // Reading: tail a file to populate a bounded channel.
    private readonly string _documentPath;
    private readonly Channel<TailEvent> _chan =
        Channel.CreateBounded<TailEvent>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly CancellationTokenSource _tailCts = new();
    private readonly SemaphoreSlim _tailStartGate = new(1, 1);
    private Task? _tailProducer;
    private volatile bool _tailStarted;
    private int _activeTails;

    // Writing: lazily start a process and write to stdin.
    private readonly string _fileName;
    private readonly string? _arguments;
    private readonly string? _workingDirectory;
    private readonly IReadOnlyDictionary<string, string?>? _environment;
    private readonly Action<ProcessStartInfo>? _configurePsi;

    private readonly SemaphoreSlim _writeStartGate = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Process? _proc;
    private Task? _procExitTask;
    private volatile bool _procStarted;

    private volatile bool _disposed;

    internal ProcessDocumentTailMux(
        string documentPath,
        string fileName,
        string? arguments = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<ProcessStartInfo>? configurePsi = null)
    {
        _documentPath = documentPath;
        _fileName = fileName;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
        _environment = environment;
        _configurePsi = configurePsi;
    }

    /// <summary>
    /// Alternate constructor that accepts an already-created process. The mux will not start the process, but will
    /// assume ownership of its lifecycle (e.g., terminate on dispose) and write to its stdin while tailing
    /// <paramref name="documentPath"/>.
    /// </summary>
    internal ProcessDocumentTailMux(
        string documentPath,
        Process process)
    {
        _documentPath = documentPath;
        _fileName = process.StartInfo.FileName;
        _arguments = process.StartInfo.Arguments;
        _workingDirectory = process.StartInfo.WorkingDirectory;
        _environment = null;
        _configurePsi = null;

        _proc = process;
        _procStarted = true;
        try
        {
            _procExitTask = _proc.WaitForExitAsync(_tailCts.Token);
        }
        catch
        {
            // ignore; tests or callers may not require exit tracking
        }
    }

    // Removed dynamic path resolver; callers should provide a concrete document path.

    public async Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureTailerStartedAsync().ConfigureAwait(false);

        if (Interlocked.Increment(ref _activeTails) != 1)
        {
            Interlocked.Decrement(ref _activeTails);
            throw new InvalidOperationException("Only one TailAsync reader is supported at a time.");
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _tailCts.Token);
            var token = linked.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var item = await _chan.Reader.ReadAsync(token).AsTask().ConfigureAwait(false);
                    await onMessage(item).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeTails);
        }
    }

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureProcessStartedAsync().ConfigureAwait(false);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var p = _proc!;
            if (p.HasExited) throw new InvalidOperationException("Process has already exited.");
            if (p.StandardInput is null) throw new InvalidOperationException("Process stdin is not available.");

            try
            {
                await p.StandardInput.WriteLineAsync(line).WaitAsync(ct).ConfigureAwait(false);
                await p.StandardInput.FlushAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception) when (_disposed || p.HasExited)
            {
                throw new InvalidOperationException("Process exited during write.");
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try { _tailCts.Cancel(); } catch { }
        try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }

        try { if (_tailProducer is not null) await _tailProducer.ConfigureAwait(false); } catch { }
        try { if (_procExitTask is not null) await _procExitTask.ConfigureAwait(false); } catch { }

        try { _chan.Writer.TryComplete(); } catch { }
        _writeLock.Dispose();
        _writeStartGate.Dispose();
        _tailStartGate.Dispose();
        _tailCts.Dispose();
        _proc?.Dispose();
    }

    // -------- internals --------
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProcessDocumentTailMux));
    }

    private async Task EnsureTailerStartedAsync()
    {
        if (_tailStarted) return;
        await _tailStartGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_tailStarted) return;
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessDocumentTailMux));

            _tailProducer = Task.Run(() => FileTailProducerAsync(_tailCts.Token), _tailCts.Token);
            _tailStarted = true;
        }
        finally
        {
            _tailStartGate.Release();
        }
    }

    private async Task EnsureProcessStartedAsync()
    {
        if (_procStarted) return;
        await _writeStartGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_procStarted) return;
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessDocumentTailMux));

            var psi = new ProcessStartInfo(_fileName, _arguments ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(_workingDirectory))
                psi.WorkingDirectory = _workingDirectory!;

            if (_environment is not null)
            {
                foreach (var (k, v) in _environment)
                {
                    if (v is null) psi.Environment.Remove(k);
                    else psi.Environment[k] = v;
                }
            }

            _configurePsi?.Invoke(psi);

            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true; // required for writes
            psi.RedirectStandardOutput = false; // not used; we tail a file instead
            psi.RedirectStandardError = false;  // not used; we tail a file instead

            _proc = new Process { StartInfo = psi };
            if (!_proc.Start())
                throw new InvalidOperationException("Failed to start process.");

            _procExitTask = _proc.WaitForExitAsync(_tailCts.Token);
            _procStarted = true;
        }
        finally
        {
            _writeStartGate.Release();
        }
    }

    private async Task FileTailProducerAsync(CancellationToken ct)
    {
        // Basic tail -f implementation with polling. Starts at current end-of-file to emulate tail behavior.
        // If the file doesn't exist yet, waits until it appears.
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!File.Exists(_documentPath))
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                    continue;
                }

                using var fs = new FileStream(
                    _documentPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                using var reader = new StreamReader(fs);

                // Read from the start of the rollout file on first attachment so the console
                // sees the full session history, then continue tailing new content.

                // At this point the file stream is open and positioned at EOF for tailing.

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
                    if (line is null)
                    {
                        // EOF; wait for more data
                        await Task.Delay(100, ct).ConfigureAwait(false);
                        continue;
                    }

                    TailEvent ev = new Line(line, DateTimeOffset.UtcNow);
                    await _chan.Writer.WriteAsync(ev, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (ChannelClosedException)
        {
            // disposal
        }
        catch (IOException)
        {
            // transient IO; exit quietly on dispose
        }
    }

}