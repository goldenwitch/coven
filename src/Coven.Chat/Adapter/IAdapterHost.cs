// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Adapter;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Coordinates an adapter with a journal by actively draining and emitting entries.
/// The host owns the two pumps:
///  - Ingress: await entries from adapter.ReadAsync(ct) and append to IScrivener<T>
///  - Egress: tail IScrivener<T> and pass each entry to adapter.DeliverAsync(entry, ct)
/// </summary>
/// <typeparam name="T">Protocol record type stored in the journal.</typeparam>
public interface IAdapterHost<T> where T : notnull
{
    /// <summary>
    /// Run both ingress and egress loops until cancelled.
    /// Implementations should not interpret protocol semantics; adapters decide what to deliver.
    /// </summary>
    /// <param name="scrivener">The journal binding for protocol entries.</param>
    /// <param name="adapter">The transport adapter implementing ingress and egress.</param>
    /// <param name="ct">Cancellation for the lifetime of the host.</param>
    Task RunAsync(
        IScrivener<T> scrivener,
        IAdapter<T> adapter,
        CancellationToken ct = default);
}