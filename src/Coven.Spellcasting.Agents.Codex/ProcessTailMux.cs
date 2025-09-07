using System.Diagnostics;
using System.Threading.Channels;

namespace Coven.Spellcasting.Agents.Codex;

/// <summary>
/// Owns a child process and multiplexes stdout/stderr into a single bounded channel.
/// Lazy-starts on first use; tailing stops when the process exits or when canceled.
/// </summary>
internal sealed class ProcessTailMux : IAsyncDisposable
{
    // Caller-provided config (unchanged surface).
    private readonly string _fileName;
    private readonly string? _arguments;
    private readonly string? _workingDirectory;
    private readonly IReadOnlyDictionary<string, string?>? _environment;
    private readonly Action<ProcessStartInfo>? _configurePsi;

    // Bounded channel to merge stdout/stderr with backpressure.
    private readonly Channel<TailEvent> _chan =
        Channel.CreateBounded<TailEvent>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly CancellationTokenSource _internalCts = new();

    // Async locks
    private readonly SemaphoreSlim _startGate = new(1, 1); // replaces lock(_startLock)
    private readonly SemaphoreSlim _writeLock = new(1, 1); // serialize stdin writes

    // Process + worker tasks (created on first start).
    private Process? _proc;
    private Task? _stdoutProducer;
    private Task? _stderrProducer;
    private Task? _exitTask;

    // State guards.
    private volatile bool _started;
    private volatile bool _disposed;
    private int _activeTails; // enforce exactly one TailAsync reader

    internal bool HasExited => _disposed || (_started && SafeHasExited());
    internal int? ExitCode
    {
        get
        {
            if (!_started || _proc is null) return null;
            try { return _proc.HasExited ? _proc.ExitCode : (int?)null; }
            catch { return null; }
        }
    }

    internal ProcessTailMux(
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

    /// <summary>
    /// Tail merged stdout/stderr until canceled or the process exits (whichever first).
    /// </summary>
    internal async Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureStartedAsync().ConfigureAwait(false);

        if (Interlocked.Increment(ref _activeTails) != 1)
        {
            Interlocked.Decrement(ref _activeTails);
            throw new InvalidOperationException("Only one TailAsync reader is supported at a time.");
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _internalCts.Token);
            var token = linked.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var readTask = _chan.Reader.ReadAsync(token).AsTask();
                    var completed = await Task.WhenAny(readTask, _exitTask!).ConfigureAwait(false);
                    if (completed == _exitTask) break;

                    var item = await readTask.ConfigureAwait(false);
                    await onMessage(item).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_internalCts.IsCancellationRequested || _disposed)
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

    /// <summary>
    /// Writes a single line to the child process stdin (serialized; auto-starts if needed).
    /// </summary>
    internal async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureStartedAsync().ConfigureAwait(false);

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

    /// <summary>
    /// Cancels producers, kills the process if running, awaits background tasks, and disposes resources.
    /// Safe to call multiple times.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try { _internalCts.Cancel(); } catch { }
        try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }

        try { if (_stdoutProducer is not null) await _stdoutProducer.ConfigureAwait(false); } catch { }
        try { if (_stderrProducer is not null) await _stderrProducer.ConfigureAwait(false); } catch { }
        try { if (_exitTask is not null) await _exitTask.ConfigureAwait(false); } catch { }

        try { _chan.Writer.TryComplete(); } catch { }
        _writeLock.Dispose();
        _startGate.Dispose();
        _internalCts.Dispose();
        _proc?.Dispose();
    }

    // ---------------- Internals ----------------

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProcessTailMux));
    }

    private bool SafeHasExited()
    {
        try { return _proc?.HasExited == true; }
        catch { return true; }
    }

    /// <summary>
    /// Lazily start process and producer loops. Enforces redirects required for tailing.
    /// Uses SemaphoreSlim for async-friendly mutual exclusion.
    /// </summary>
    private async Task EnsureStartedAsync()
    {
        if (_started) return;
        await _startGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_started) return;
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessTailMux));

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

            // Minimum required for tailing:
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;

            _proc = new Process { StartInfo = psi };
            if (!_proc.Start())
                throw new InvalidOperationException("Failed to start process.");

            _exitTask = _proc.WaitForExitAsync(_internalCts.Token);

            _stdoutProducer = Task.Run(() => ProducerLoopAsync(_proc.StandardOutput, isError: false, _internalCts.Token), _internalCts.Token);
            _stderrProducer = Task.Run(() => ProducerLoopAsync(_proc.StandardError, isError: true, _internalCts.Token), _internalCts.Token);

            _started = true;
        }
        finally
        {
            _startGate.Release();
        }
    }

    /// <summary>
    /// Reads one stream until EOF/cancel and writes typed TailEvents into the channel.
    /// </summary>
    private async Task ProducerLoopAsync(StreamReader reader, bool isError, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
                if (line is null) break; // EOF

                TailEvent ev = isError
                    ? new ErrorLine(line, DateTimeOffset.UtcNow)
                    : new Line(line, DateTimeOffset.UtcNow);

                await _chan.Writer.WriteAsync(ev, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal during shutdown.
        }
        catch (ChannelClosedException)
        {
            // Channel closed during disposal; exit quietly.
        }
    }
}
