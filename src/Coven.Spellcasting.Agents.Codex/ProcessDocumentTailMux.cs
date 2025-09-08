using System;
using System.Diagnostics;
using System.Threading.Channels;

namespace Coven.Spellcasting.Agents.Codex;

/// <summary>
/// Asymmetric tail multiplexer that reads from a specified document (file) as the input stream
/// and writes to a lazily-started child process for output. Reading and writing paths are decoupled.
/// </summary>
internal sealed class ProcessDocumentTailMux : ITailMux
{
    // Reading: tail a file to populate a bounded channel.
    private readonly string? _documentPath;
    private readonly Func<string?>? _documentPathResolver;
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

    // Signals (for tests) that the file has been opened for reading at least once.
    private readonly TaskCompletionSource<bool> _fileOpenedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    internal ProcessDocumentTailMux(
        Func<string?> documentPathResolver,
        string fileName,
        string? arguments = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<ProcessStartInfo>? configurePsi = null)
    {
        _documentPathResolver = documentPathResolver;
        _fileName = fileName;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
        _environment = environment;
        _configurePsi = configurePsi;
    }

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
            catch (IOException) when (p.HasExited)
            {
                throw new InvalidOperationException("Process exited during write.");
            }
            catch (ObjectDisposedException) when (_disposed || p.HasExited)
            {
                throw new InvalidOperationException("Process exited during write.");
            }
            catch (InvalidOperationException) when (p.HasExited)
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
                // Resolve the path either from the fixed value or a dynamic resolver.
                var targetPath = _documentPath;
                if (string.IsNullOrWhiteSpace(targetPath) && _documentPathResolver is not null)
                {
                    try { targetPath = _documentPathResolver(); }
                    catch { targetPath = null; }
                }

                if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                    continue;
                }

                using var fs = new FileStream(
                    targetPath!,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                using var reader = new StreamReader(fs);

                // Seek to end to emulate tailing newly appended content.
                // Comment the next line to read from start of file instead.
                fs.Seek(0, SeekOrigin.End);

                // Signal readiness after the file stream is opened and positioned.
                try { _fileOpenedTcs.TrySetResult(true); } catch { }

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

    // Internal utility for tests to await until the tailer has opened the file at least once.
    internal Task WaitUntilReadyAsync(CancellationToken ct = default)
        => _fileOpenedTcs.Task.WaitAsync(ct);
}
