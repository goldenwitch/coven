// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public interface IBoard
{
    public Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null, CancellationToken cancellationToken = default);

    public Task GetWork<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink, CancellationToken cancellationToken = default);
    public bool WorkSupported<T>(List<string> tags);
}
