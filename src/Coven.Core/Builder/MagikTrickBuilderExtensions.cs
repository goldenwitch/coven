// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Routing;
using Coven.Core.Tricks;

namespace Coven.Core.Builder;

public static class MagikTrickBuilderExtensions
{
    // Registers a Trick (T->T identity fork) followed by its downstream candidates.
    // The trick fences the next selection to only those candidates. The active
    // ISelectionStrategy determines which candidate runs.
    public static IMagikBuilder<TStart, TEnd> MagikTrick<TStart, TEnd, T>(
        this IMagikBuilder<TStart, TEnd> builder,
        Action<IMagikBuilder<TStart, TEnd>> configureCandidates,
        IEnumerable<string>? trickCapabilities = null)
    {
        var trick = new MagikTrick<T>(trickCapabilities);
        if (trickCapabilities is null) builder.MagikBlock<T, T>(trick);
        else builder.MagikBlock<T, T>(trick, trickCapabilities);

        // Wrap builder so that any candidate registered with input T is recorded for the fence
        var collected = new List<MagikTrick<T>.CandidateRef>();
        var proxy = new TrickCandidatesProxy<TStart, TEnd, T>(builder, collected);
        configureCandidates(proxy);
        trick.SetCandidates(collected);
        return builder;
    }

    // Proxy builder that records all candidates with input type T so the Trick can fence the next selection
    internal sealed class TrickCandidatesProxy<TStart, TEnd, T> : IMagikBuilder<TStart, TEnd>
    {
        private readonly IMagikBuilder<TStart, TEnd> inner;
        private readonly List<MagikTrick<T>.CandidateRef> collected;

        public TrickCandidatesProxy(IMagikBuilder<TStart, TEnd> inner, List<MagikTrick<T>.CandidateRef> collected)
        {
            this.inner = inner;
            this.collected = collected;
        }

        public IMagikBuilder<TStart, TEnd> UseSelectionStrategy(ISelectionStrategy strategy)
        {
            inner.UseSelectionStrategy(strategy);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null)
        {
            if (typeof(TIn) == typeof(T)) collected.Add(new MagikTrick<T>.CandidateRef { Instance = block!, Capabilities = (capabilities?.ToList()) ?? new List<string>() });
            inner.MagikBlock<TIn, TOut>(block, capabilities);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func, IEnumerable<string>? capabilities = null)
        {
            if (typeof(TIn) == typeof(T))
            {
                var mb = new MagikBlock<TIn, TOut>(func);
                collected.Add(new MagikTrick<T>.CandidateRef { Instance = mb!, Capabilities = (capabilities?.ToList()) ?? new List<string>() });
                inner.MagikBlock<TIn, TOut>(mb, capabilities);
            }
            else inner.MagikBlock<TIn, TOut>(func, capabilities);
            return this;
        }

        public ICoven Done() => inner.Done();
        public ICoven Done(bool pull, PullOptions? pullOptions = null) => inner.Done(pull, pullOptions);
    }
}