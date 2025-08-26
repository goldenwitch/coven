using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coven.Core;

// Concrete orchestrator for Pull mode that advances work step-by-step by
// calling IBoard.GetWork<TIn>(GetWorkRequest<TIn>, IOrchestratorSink) until
// the produced value is assignable to the requested TOut.
internal sealed class PullOrchestrator : IOrchestratorSink
{
    private readonly Board board;
    private readonly string branchId;
    private TaskCompletionSource<object>? finalTcs;
    private IReadOnlyCollection<string>? initialTags;
    private bool usedInitialTags;
    private Type? expectedFinalType;
    private readonly PullOptions? options;

    internal PullOrchestrator(Board board, PullOptions? options = null, string branchId = "main")
    {
        this.board = board;
        this.options = options;
        this.branchId = branchId;
    }

    public void Complete<TStepOut>(TStepOut output, string? branchId = null)
    {
        // Use declared generic types to determine finality, not runtime value type.
        // If the step's declared TStepOut is assignable to the requested final type, consult completion policy.
        if (expectedFinalType is not null && expectedFinalType.IsAssignableFrom(typeof(TStepOut)))
        {
            if (options?.ShouldComplete is not null)
            {
                if (options.ShouldComplete(output!))
                {
                    CompletedFinal(output!);
                }
                else
                {
                    _ = NextAsync(output);
                }
                return;
            }

            // Default behavior: complete when assignable
            CompletedFinal(output!);
            return;
        }

        _ = NextAsync(output);
    }

    public void CompletedFinal<TFinal>(TFinal result)
    {
        finalTcs?.TrySetResult((object)result!);
    }

    internal async Task<TOut> Run<TIn, TOut>(TIn input, List<string>? initialTags = null)
    {
        // Initial finality is also based on declared types: is TIn assignable to TOut?
        if (typeof(TOut).IsAssignableFrom(typeof(TIn)))
        {
            // Consult per-step policy first; then fallback to initial-input policy; default is to complete immediately
            if (options?.ShouldComplete is not null)
            {
                if (options.ShouldComplete(input!))
                    return (TOut)(object)input!;
                // else fall through and start stepping
            }
            else if (options?.IsInitialComplete is not null)
            {
                if (options.IsInitialComplete(input!))
                    return (TOut)(object)input!;
                // else fall through and start stepping
            }
            else
            {
                return (TOut)(object)input!;
            }
        }

        this.initialTags = initialTags as IReadOnlyCollection<string>;
        this.usedInitialTags = false;
        this.expectedFinalType = typeof(TOut);
        finalTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = NextAsync(input);

        var result = await finalTcs.Task.ConfigureAwait(false);
        return (TOut)result;
    }

    private async Task NextAsync<TCur>(TCur current)
    {
        try
        {
            var tags = usedInitialTags ? null : initialTags;
            usedInitialTags = true;
            await board.GetWork(new GetWorkRequest<TCur>(current, tags, branchId), this).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            finalTcs?.TrySetException(ex);
        }
    }
}
