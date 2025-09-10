using System.Collections.Concurrent;
using Coven.Durables;

namespace Coven.Sophia.Tests.Helpers;

internal sealed class TestLogStorage : IDurableList<string>
{
    private readonly SimpleFileStorage<string> inner;
    public Action<string>? OnAppend { get; set; }
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> waiters = new(StringComparer.Ordinal);

    public TestLogStorage(string path, Action<string>? onAppend = null)
    {
        inner = new SimpleFileStorage<string>(path);
        OnAppend = onAppend;
    }

    public async Task Append(string item)
    {
        await inner.Append(item).ConfigureAwait(false);
        try { OnAppend?.Invoke(item); } catch { /* test hook only */ }
        try
        {
            foreach (var kv in waiters)
            {
                if (item.Contains(kv.Key, StringComparison.Ordinal))
                {
                    if (waiters.TryRemove(kv.Key, out var tcs)) tcs.TrySetResult(true);
                }
            }
        }
        catch { /* test hook only */ }
    }

    public Task Save(List<string> input) => inner.Save(input);
    public Task<List<string>> Load() => inner.Load();

    public Task<bool> WaitForContainsAsync(string text)
    {
        var tcs = waiters.GetOrAdd(text, _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        // Best-effort immediate satisfy if already present on disk
        _ = TrySatisfyFromExistingAsync(text, tcs);
        return tcs.Task;
    }

    private async Task TrySatisfyFromExistingAsync(string text, TaskCompletionSource<bool> tcs)
    {
        try
        {
            var existing = await inner.Load().ConfigureAwait(false);
            if (existing.Any(e => e.Contains(text, StringComparison.Ordinal)))
            {
                if (waiters.TryRemove(text, out var found)) found.TrySetResult(true);
            }
        }
        catch { /* ignore */ }
    }
}
