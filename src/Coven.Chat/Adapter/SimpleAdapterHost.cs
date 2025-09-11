// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Adapter;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

    public sealed class SimpleAdapterHost<T> : IAdapterHost<T> where T : notnull
    {
        private readonly ILogger<SimpleAdapterHost<T>> _log;

    public SimpleAdapterHost() : this(NullLogger<SimpleAdapterHost<T>>.Instance) { }
    public SimpleAdapterHost(ILogger<SimpleAdapterHost<T>> logger)
    {
        _log = logger ?? NullLogger<SimpleAdapterHost<T>>.Instance;
    }

    public async Task RunAsync(
        IScrivener<T> scrivener,
        IAdapter<T> adapter,
        CancellationToken ct = default)
    {
        if (scrivener is null) throw new ArgumentNullException(nameof(scrivener));
        if (adapter is null) throw new ArgumentNullException(nameof(adapter));

        // Correlate this host session with a conversation id for breadcrumbs.
        var cid = Guid.NewGuid().ToString("N");
        using var scope = _log.BeginScope($"cid:{cid}");
        _log.LogInformation("Chat begin cid={ConversationId} type={EntryType}", cid, typeof(T).Name);

        var ingress = Task.Run(async () =>
        {
            _log.LogDebug("Ingress start cid={ConversationId}", cid);
            await foreach (var entry in adapter.ReadAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var pos = await scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
                    _log.LogInformation("Ingress append pos={Pos} type={Type} cid={ConversationId}", pos, entry.GetType().Name, cid);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _log.LogWarning(ex, "Ingress error cid={ConversationId}", cid); }
            }
        }, ct);

        var egress = Task.Run(async () =>
        {
            _log.LogDebug("Egress start cid={ConversationId}", cid);
            long after = 0;
            await foreach (var pair in scrivener.TailAsync(after, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                after = pair.journalPosition;
                try
                {
                    var entry = pair.entry; // T is constrained notnull
                    _log.LogInformation("Egress deliver pos={Pos} type={Type} next={Next} cid={ConversationId}", after, entry.GetType().Name, after + 1, cid);
                    await adapter.DeliverAsync(entry, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _log.LogWarning(ex, "Egress error cid={ConversationId}", cid); }
            }
        }, ct);

        try { await Task.WhenAll(ingress, egress).ConfigureAwait(false); }
        catch (OperationCanceledException) { _log.LogInformation("Chat cancelled cid={ConversationId}", cid); }
        finally { _log.LogInformation("Chat end cid={ConversationId}", cid); }
    }
}