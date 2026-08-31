// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Coven.Ui.Desktop.Logging;

/// <summary>
/// Minimal file logging provider.
/// </summary>
/// <remarks>
/// <para>
/// The application is a <c>WinExe</c>, so console logging is written to a console that does
/// not exist. Every breadcrumb the Coven leaves emit — observed, transmuted, appended, at each
/// hop — was being discarded, which made a stalled turn impossible to diagnose.
/// </para>
/// <para>
/// The sink is static and shared: a session rebuild disposes one host and builds another, and
/// two providers holding the same file open would contend.
/// </para>
/// </remarks>
internal sealed class FileLoggerProvider(string filePath) : ILoggerProvider
{
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath, categoryName);

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);
}

internal sealed class FileLogger(string filePath, string category) : ILogger
{
    private static readonly Lock _writeGate = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);

        StringBuilder line = new();
        line.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(Abbreviate(logLevel)).Append("] ")
            .Append(category)
            .Append(" - ")
            .Append(formatter(state, exception));

        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        line.AppendLine();

        try
        {
            lock (_writeGate)
            {
                File.AppendAllText(filePath, line.ToString());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the application down.
        }
    }

    private static string Abbreviate(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };
}

/// <summary>
/// Resolves and prepares the log file location.
/// </summary>
internal static class AppLog
{
    /// <summary>Directory holding application logs.</summary>
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "Coven",
        "logs");

    /// <summary>Current log file, one per day.</summary>
    public static string FilePath { get; } = Path.Combine(
        Directory,
        FormattableString.Invariant($"coven-ui-{DateTime.Now:yyyy-MM-dd}.log"));

    /// <summary>Creates the log directory. Safe to call repeatedly.</summary>
    public static void Prepare()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging is best effort.
        }
    }
}
