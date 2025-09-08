namespace Coven.Chat.Adapter;

using System.Threading;
using System.Threading.Tasks;

public sealed class SimpleAdapterHost<T> : IAdapterHost<T> where T : notnull
{
    public async Task RunAsync(
        IScrivener<T> scrivener,
        IAdapter<T> adapter,
        CancellationToken ct = default)
    {
        if (scrivener is null) throw new ArgumentNullException(nameof(scrivener));
        if (adapter is null) throw new ArgumentNullException(nameof(adapter));

        // Run ingress (adapter -> journal) and egress (journal -> adapter) concurrently.
        var ingress = Task.Run(async () =>
        {
            await foreach (var entry in adapter.ReadAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                try { _ = await scrivener.WriteAsync(entry, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
        }, ct);

        var egress = Task.Run(async () =>
        {
            long after = 0;
            await foreach (var pair in scrivener.TailAsync(after, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                after = pair.journalPosition;
                try { await adapter.DeliverAsync(pair.entry, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
        }, ct);

        try { await Task.WhenAll(ingress, egress).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }
}

