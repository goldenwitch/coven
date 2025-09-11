// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Sophia;

internal sealed class SophiaLogger : ILogger
{
    private readonly string category;
    private readonly SophiaLoggerOptions options;
    private IExternalScopeProvider? scopeProvider;
    private readonly SophiaLoggerProvider provider;

    public SophiaLogger(string category, SophiaLoggerOptions options, IExternalScopeProvider? scopeProvider, SophiaLoggerProvider provider)
    {
        this.category = category;
        this.options = options;
        this.scopeProvider = scopeProvider;
        this.provider = provider;
    }

    public void SetScopeProvider(IExternalScopeProvider? provider) => scopeProvider = provider;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        if (!options.IncludeScopes)
        {
            return NullScope.Instance;
        }
        return scopeProvider?.Push(state!) ?? NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= options.MinimumLevel && logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var now = DateTimeOffset.UtcNow;
        var label = string.IsNullOrWhiteSpace(options.Label) ? "sophia" : options.Label;
        var message = formatter is not null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
        string entry;

        entry = $"[{now:o}] {label} {logLevel} {category} ({eventId.Id}) :: {message}";
        if (exception is not null)
        {
            entry += $" | Exception: {exception}";
        }

        if (options.IncludeScopes && scopeProvider is not null)
        {
            try
            {
                var scopes = new List<string>();
                scopeProvider.ForEachScope((s, acc) => acc.Add(s?.ToString() ?? string.Empty), scopes);
                if (scopes.Count > 0)
                {
                    entry += $" | Scopes=[{string.Join(", ", scopes)}]";
                }
            }
            catch
            {
                // Ignore scope enumeration failures
            }
        }

        // Queue for asynchronous durable append handled by provider background task
        provider.Enqueue(entry);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}