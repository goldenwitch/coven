// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Ui.Desktop.Local;

/// <summary>
/// Passes llama.cpp log messages through to a real logger while remembering the last error.
/// </summary>
/// <remarks>
/// llama.cpp explains its own failures precisely — <c>unknown model architecture</c>,
/// <c>missing tensor 'blk.64.ssm_conv1d.weight'</c>, allocation failures — but only on its
/// native log. LLamaSharp turns all of them into the same
/// <c>Failed to load model '&lt;path&gt;'</c>, so without capturing the message here the one
/// sentence that identifies the problem never reaches the person who needs it.
/// </remarks>
internal sealed class NativeErrorCapture(ILogger inner) : ILogger
{
    private static readonly Lock _gate = new();
    private static string? _last;

    private readonly ILogger _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>The most recent error message, or <see langword="null"/> when there is none.</summary>
    public static string? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    /// <summary>Forgets the last error, so a stale one is not attributed to a fresh attempt.</summary>
    public static void Reset()
    {
        lock (_gate)
        {
            _last = null;
        }
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (logLevel >= LogLevel.Error)
        {
            string message = formatter(state, exception).Trim();

            // llama.cpp emits a cascade: the specific cause first, then generic wrappers such
            // as "failed to load model". Keeping the first of a run preserves the useful one.
            if (message.Length > 0)
            {
                lock (_gate)
                {
                    _last ??= message;
                }
            }
        }

        _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
