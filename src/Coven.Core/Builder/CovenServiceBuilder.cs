// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tags;
using Coven.Core.Activation;
using Coven.Core.Routing;

namespace Coven.Core.Builder;

/// <summary>
/// Fluent builder used to register MagikBlocks and finalize the Coven runtime.
/// </summary>
public sealed class CovenServiceBuilder
{
    private readonly IServiceCollection _services;
    private readonly MagikRegistry _registry = new();
    private bool finalized;

    internal CovenServiceBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers a MagikBlock type for the specified input/output pair.
    /// </summary>
    /// <typeparam name="TIn">Input type.</typeparam>
    /// <typeparam name="TOut">Output type.</typeparam>
    /// <typeparam name="TBlock">Concrete block implementing <see cref="IMagikBlock{T, TOutput}"/>.</typeparam>
    /// <param name="lifetime">DI lifetime for the block type; transient by default.</param>
    /// <param name="capabilities">Optional capability tags that influence selection.</param>
    /// <returns>The same builder for chaining.</returns>
    public CovenServiceBuilder MagikBlock<TIn, TOut, TBlock>(ServiceLifetime lifetime = ServiceLifetime.Transient, IEnumerable<string>? capabilities = null)
        where TBlock : class, IMagikBlock<TIn, TOut>
    {
        EnsureNotFinalized();
        bool already = false;
        foreach (ServiceDescriptor sd in _services)
        {
            if (sd.ServiceType == typeof(TBlock)) { already = true; break; }
        }
        if (!already)
        {
            _services.Add(new ServiceDescriptor(typeof(TBlock), typeof(TBlock), lifetime));
        }
        // Merge capabilities from caller and attribute only (DI-level source of truth)
        List<string> mergedCaps = [];
        if (capabilities is not null)
        {
            mergedCaps.AddRange(capabilities);
        }

        Type t = typeof(TBlock);
        TagCapabilitiesAttribute? attr = (TagCapabilitiesAttribute?)Attribute.GetCustomAttribute(t, typeof(TagCapabilitiesAttribute));
        if (attr is not null && attr.Tags is not null)
        {
            mergedCaps.AddRange(attr.Tags);
        }
        // Register with a DI activator; no proxy instance.
        DiTypeActivator activator = new(typeof(TBlock));
        object placeholder = new();
        _registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), placeholder, mergedCaps, typeof(TBlock).Name, activator));
        return this;
    }

    /// <summary>
    /// Overrides the default selection strategy used to choose blocks at runtime.
    /// </summary>
    /// <param name="strategy">The selection strategy.</param>
    /// <returns>The same builder for chaining.</returns>
    public CovenServiceBuilder UseSelectionStrategy(ISelectionStrategy strategy)
    {
        _registry.SetSelectionStrategy(strategy);
        return this;
    }

    /// <summary>
    /// Registers an inline lambda-based MagikBlock for the specified input/output pair.
    /// </summary>
    /// <typeparam name="TIn">Input type.</typeparam>
    /// <typeparam name="TOut">Output type.</typeparam>
    /// <param name="func">Async function invoked to perform the work.</param>
    /// <param name="capabilities">Optional capability tags that influence selection.</param>
    /// <returns>The same builder for chaining.</returns>
    public CovenServiceBuilder LambdaBlock<TIn, TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        ArgumentNullException.ThrowIfNull(func);
        EnsureNotFinalized();
        MagikBlock<TIn, TOut> mb = new(func);
        _registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), mb, capabilities?.ToList(), typeof(MagikBlock<TIn, TOut>).Name, null));
        return this;
    }

    /// <summary>
    /// Finalizes the builder and registers the runtime services.
    /// </summary>
    /// <param name="pull">When true, creates a pull-oriented board.</param>
    /// <param name="pullOptions">Optional configuration for pull mode.</param>
    /// <returns>The service collection used by the builder.</returns>
    public IServiceCollection Done(bool pull = false, PullOptions? pullOptions = null)
    {
        if (finalized)
        {
            return _services; // idempotent
        }


        finalized = true;

        Board board = _registry.BuildBoard(pull, pullOptions);
        _services.AddSingleton<IBoard>(_ => board);
        _services.AddSingleton<ICoven>(sp => new Coven(board, sp));
        return _services;
    }

    private void EnsureNotFinalized()
    {
        if (finalized)
        {
            throw new InvalidOperationException("Cannot modify CovenServiceBuilder after Done().");
        }

    }
}

