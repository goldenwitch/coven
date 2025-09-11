// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Coven.Core.Tests.Infrastructure;

internal sealed class InMemoryLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? scopeProvider;
    private readonly ConcurrentQueue<string> entries = new();
    private bool disposed;

    public IReadOnlyCollection<string> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        if (disposed) throw new ObjectDisposedException(nameof(InMemoryLoggerProvider));
        return new InMemoryLogger(categoryName, this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
        disposed = true;
        while (entries.TryDequeue(out _)) { }
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly string category;
        private readonly InMemoryLoggerProvider owner;

        public InMemoryLogger(string category, InMemoryLoggerProvider owner)
        {
            this.category = category;
            this.owner = owner;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter is not null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
            var line = $"[{logLevel}] {category} :: {msg}";
            if (exception is not null) line += $" | ex={exception.GetType().Name}";
            owner.entries.Enqueue(line);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
