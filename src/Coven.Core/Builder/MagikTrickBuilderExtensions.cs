using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Tricks;

namespace Coven.Core.Builder;

public static class MagikTrickBuilderExtensions
{
    // Registers a Trick (T->T identity fork) followed by its downstream candidates.
    // The trick emits arbitrary tags chosen by the lambda, and routing is restricted
    // to the trick's candidates using a unique capability token.
    public static IMagikBuilder<TStart, TEnd> MagikTrick<TStart, TEnd, T>(
        this IMagikBuilder<TStart, TEnd> builder,
        Func<ISet<string>, T, IEnumerable<string>?> chooseTags,
        Action<IMagikBuilder<TStart, TEnd>> configureCandidates,
        IEnumerable<string>? trickCapabilities = null)
    {
        var trick = new MagikTrick<T>(chooseTags, trickCapabilities);
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

        public IMagikBuilder<TStart, TEnd> MagikBlock(IMagikBlock<TStart, TEnd> block)
        {
            if (typeof(TStart) == typeof(T)) collected.Add(new MagikTrick<T>.CandidateRef { Instance = block!, Capabilities = new List<string>() });
            inner.MagikBlock(block);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock(Func<TStart, Task<TEnd>> func)
        {
            if (typeof(TStart) == typeof(T))
            {
                var mb = new MagikBlock<TStart, TEnd>(func);
                collected.Add(new MagikTrick<T>.CandidateRef { Instance = mb!, Capabilities = new List<string>() });
                inner.MagikBlock(mb);
            }
            else inner.MagikBlock(func);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock(IMagikBlock<TStart, TEnd> block, IEnumerable<string> capabilities)
        {
            if (typeof(TStart) == typeof(T)) collected.Add(new MagikTrick<T>.CandidateRef { Instance = block!, Capabilities = capabilities.ToList() });
            inner.MagikBlock(block, capabilities);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock(Func<TStart, Task<TEnd>> func, IEnumerable<string> capabilities)
        {
            if (typeof(TStart) == typeof(T))
            {
                var mb = new MagikBlock<TStart, TEnd>(func);
                collected.Add(new MagikTrick<T>.CandidateRef { Instance = mb!, Capabilities = capabilities.ToList() });
                inner.MagikBlock(mb, capabilities);
            }
            else inner.MagikBlock(func, capabilities);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block)
        {
            if (typeof(TIn) == typeof(T)) collected.Add(new MagikTrick<T>.CandidateRef { Instance = block!, Capabilities = new List<string>() });
            inner.MagikBlock(block);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func)
        {
            if (typeof(TIn) == typeof(T))
            {
                var mb = new MagikBlock<TIn, TOut>(func);
                collected.Add(new MagikTrick<T>.CandidateRef { Instance = mb!, Capabilities = new List<string>() });
                inner.MagikBlock(mb);
            }
            else inner.MagikBlock(func);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string> capabilities)
        {
            if (typeof(TIn) == typeof(T)) collected.Add(new MagikTrick<T>.CandidateRef { Instance = block!, Capabilities = capabilities.ToList() });
            inner.MagikBlock(block, capabilities);
            return this;
        }

        public IMagikBuilder<TStart, TEnd> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func, IEnumerable<string> capabilities)
        {
            if (typeof(TIn) == typeof(T))
            {
                var mb = new MagikBlock<TIn, TOut>(func);
                collected.Add(new MagikTrick<T>.CandidateRef { Instance = mb!, Capabilities = capabilities.ToList() });
                inner.MagikBlock(mb, capabilities);
            }
            else inner.MagikBlock(func, capabilities);
            return this;
        }

        public ICoven Done() => inner.Done();
        public ICoven Done(bool pull) => inner.Done(pull);
        public ICoven Done(bool pull, PullOptions? pullOptions = null) => inner.Done(pull, pullOptions);
    }
}
