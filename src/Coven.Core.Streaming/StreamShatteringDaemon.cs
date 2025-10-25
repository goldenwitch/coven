// SPDX-License-Identifier: BUSL-1.1
using Coven.Daemonology;

namespace Coven.Core.Streaming;

public sealed class StreamShatteringDaemon<TEntry, TSource>(
    IScrivener<DaemonEvent> daemonEvents,
    IScrivener<TEntry> journal,
    IShatterPolicy<TEntry> shatterPolicy
) : ContractDaemon(daemonEvents), IAsyncDisposable
    where TEntry : notnull
    where TSource : TEntry
{
    private readonly IScrivener<TEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly IShatterPolicy<TEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private CancellationTokenSource? _linkedCancellationSource;
    private Task? _pumpTask;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken linkedToken = _linkedCancellationSource.Token;

        long startPosition = 0;
        await foreach ((long position, _) in _journal.ReadBackwardAsync(long.MaxValue, linkedToken))
        {
            startPosition = position;
            break;
        }

        _pumpTask = Task.Run(() => RunAsync(startPosition, linkedToken), linkedToken);
        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    public override async Task Shutdown(CancellationToken cancellationToken)
    {
        _linkedCancellationSource?.Cancel();
        if (_pumpTask is not null)
        {
            try
            {
                await _pumpTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // cooperative shutdown
            }
            finally
            {
                _pumpTask = null;
            }
        }
        await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(long startAfterPosition, CancellationToken cancellationToken)
    {
        try
        {
            await foreach ((_, TEntry entry) in _journal.TailAsync(startAfterPosition, cancellationToken))
            {
                if (entry is not TSource source)
                {
                    continue; // only scatter the designated source type
                }

                IEnumerable<TEntry> outputs = _shatterPolicy.Shatter(source) ?? [];
                foreach (TEntry output in outputs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _journal.WriteAsync(output, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // cooperative shutdown
        }
        catch (Exception ex)
        {
            await Fail(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Status != Status.Completed)
            {
                await Shutdown(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _linkedCancellationSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
