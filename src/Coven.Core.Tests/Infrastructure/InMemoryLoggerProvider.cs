// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Coven.Core.Tests.Infrastructure;

internal sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();
    private bool disposed;

    public IReadOnlyCollection<string> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName)
    {
        return disposed ? throw new ObjectDisposedException(nameof(InMemoryLoggerProvider)) : (ILogger)new InMemoryLogger(categoryName, this);
    }

    public void Dispose()
    {
        disposed = true;
        while (_entries.TryDequeue(out _)) { }
    }

    private sealed class InMemoryLogger(string category, InMemoryLoggerProvider owner) : ILogger
    {

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope._instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string msg = formatter is not null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
            string line = $"[{logLevel}] {category} :: {msg}";
            if (exception is not null)
            {
                line += $" | ex={exception.GetType().Name}";
            }


            owner._entries.Enqueue(line);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope _instance = new();
            public void Dispose() { }
        }
    }
}
