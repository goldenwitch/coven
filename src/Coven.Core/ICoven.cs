// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public interface ICoven
{
    // Runs a ritual from T to TOutput with no initial tags
    public Task<TOutput> Ritual<T, TOutput>(T input, CancellationToken cancellationToken = default);

    // Runs a ritual from T to TOutput seeding initial tags that influence routing
    public Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags, CancellationToken cancellationToken = default);

    public Task<TOutput> Ritual<TOutput>(CancellationToken cancellationToken = default);
}
