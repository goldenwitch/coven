// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Covenants;

/// <summary>
/// Builder implementation that collects covenant metadata and wires DI registrations.
/// </summary>
/// <typeparam name="TCovenant">The covenant being built.</typeparam>
/// <param name="services">The service collection to register into.</param>
public sealed class StreamingCovenantBuilder<TCovenant>(IServiceCollection services) : IStreamingCovenantBuilder<TCovenant>
    where TCovenant : ICovenant
{
    private readonly IServiceCollection _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// The collected graph metadata.
    /// </summary>
    public CovenantGraph<TCovenant> Graph { get; } = new();

    /// <inheritdoc />
    public IStreamingCovenantBuilder<TCovenant> Source<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSource<TCovenant>
    {
        Graph.AddSource(typeof(TEntry));
        return this;
    }

    /// <inheritdoc />
    ICovenantBuilder<TCovenant> ICovenantBuilder<TCovenant>.Source<TEntry>() => Source<TEntry>();

    /// <inheritdoc />
    public IStreamingCovenantBuilder<TCovenant> Sink<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSink<TCovenant>
    {
        Graph.AddSink(typeof(TEntry));
        return this;
    }

    /// <inheritdoc />
    ICovenantBuilder<TCovenant> ICovenantBuilder<TCovenant>.Sink<TEntry>() => Sink<TEntry>();

    /// <inheritdoc />
    public IStreamingCovenantBuilder<TCovenant> Window<TChunk, TOutput>(
        IWindowPolicy<TChunk> policy,
        IBatchTransmuter<TChunk, TOutput> transmuter,
        IShatterPolicy<TOutput>? shatter = null)
        where TChunk : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(transmuter);

        Graph.AddWindow(typeof(TChunk), typeof(TOutput), policy, transmuter, shatter);

        // Register the components for DI resolution
        _services.AddSingleton(policy);
        _services.AddSingleton(transmuter);
        if (shatter is not null)
        {
            _services.AddSingleton(shatter);
        }

        return this;
    }

    /// <inheritdoc />
    public IStreamingCovenantBuilder<TCovenant> Transform<TInput, TOutput>(
        ITransmuter<TInput, TOutput> transmuter)
        where TInput : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>
    {
        ArgumentNullException.ThrowIfNull(transmuter);

        Graph.AddTransform(typeof(TInput), typeof(TOutput), transmuter);

        // Register the transmuter for DI resolution
        _services.AddSingleton(transmuter);

        return this;
    }

    /// <inheritdoc />
    public IStreamingCovenantBuilder<TCovenant> Junction<TIn>(
        Action<IJunctionBuilder<TCovenant, TIn>> configure)
        where TIn : ICovenantEntry<TCovenant>
    {
        ArgumentNullException.ThrowIfNull(configure);

        JunctionBuilder<TCovenant, TIn> builder = new();
        configure(builder);

        if (builder.Routes.Count == 0 && builder.FallbackRoute is null)
        {
            throw new InvalidOperationException("Junction must have at least one route or a fallback.");
        }

        Graph.AddJunction(typeof(TIn), builder.Routes, builder.FallbackRoute);

        return this;
    }

    /// <summary>
    /// Validates the covenant graph at runtime.
    /// Called automatically by <see cref="CovenantServiceCollectionExtensions.AddCovenant{TCovenant}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the graph is invalid.</exception>
    public void Validate()
    {
        CovenantValidator.Validate(Graph);
    }
}
