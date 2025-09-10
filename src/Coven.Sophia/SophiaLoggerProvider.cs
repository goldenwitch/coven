using Microsoft.Extensions.Logging;
using Coven.Durables;
using System.Collections.Concurrent;
using System.Threading;

namespace Coven.Sophia;

public sealed class SophiaLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly IDurableList<string> storage;
    private readonly SophiaLoggerOptions options;
    private IExternalScopeProvider? scopeProvider;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SophiaLogger> cache = new(StringComparer.Ordinal);
    private bool disposed;
    private readonly ConcurrentQueue<string> queue = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly CancellationTokenSource cts = new();
    private readonly Task background;

    public SophiaLoggerProvider(IDurableList<string> storage, SophiaLoggerOptions options)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        background = Task.Run(ProcessAsync);
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SophiaLoggerProvider));
        return cache.GetOrAdd(categoryName, name => new SophiaLogger(name, options, scopeProvider, this));
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.scopeProvider = scopeProvider;
        foreach (var kvp in cache)
        {
            kvp.Value.SetScopeProvider(scopeProvider);
        }
    }

    public void Dispose()
    {
        disposed = true;
        try { cts.Cancel(); } catch { }
        try { signal.Release(); } catch { }
        try { background.Wait(1000); } catch { }
        cts.Dispose();
        signal.Dispose();
    }

    internal void Enqueue(string entry)
    {
        if (disposed) return;
        queue.Enqueue(entry);
        try { signal.Release(); } catch { }
    }

    private async Task ProcessAsync()
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                await signal.WaitAsync(cts.Token).ConfigureAwait(false);
                while (queue.TryDequeue(out var item))
                {
                    try { await storage.Append(item).ConfigureAwait(false); }
                    catch { /* swallow logging errors */ }
                }
            }
        }
        catch (OperationCanceledException) { }
        // Drain on cancellation
        while (queue.TryDequeue(out var item2))
        {
            try { await storage.Append(item2).ConfigureAwait(false); } catch { }
        }
    }
}
