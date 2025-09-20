// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public interface IBoard
{
    Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null, CancellationToken cancellationToken = default);

    Task GetWork<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink, CancellationToken cancellationToken = default);
    bool WorkSupported<T>(List<string> tags);
}
