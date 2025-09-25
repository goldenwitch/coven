// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public interface ICoven
{
    // Runs a ritual from T to TOutput with no initial tags
    Task<TOutput> Ritual<T, TOutput>(T input, CancellationToken cancellationToken = default);

    // Runs a ritual from T to TOutput seeding initial tags that influence routing
    Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags, CancellationToken cancellationToken = default);

    Task<TOutput> Ritual<TOutput>(CancellationToken cancellationToken = default);
}
