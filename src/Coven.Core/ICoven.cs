// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public interface ICoven
{
    // Runs a ritual from T to TOutput with no initial tags
    public Task<TOutput> Ritual<T, TOutput>(T input);

    // Runs a ritual from T to TOutput seeding initial tags that influence routing
    public Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags);

    public Task<TOutput> Ritual<TOutput>();
}