// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tags;
using Coven.Core.Activation;
using Coven.Core.Routing;

namespace Coven.Core.Builder;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection BuildCoven(this IServiceCollection services, Action<CovenServiceBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(build);

        CovenServiceBuilder builder = new(services);
        build(builder);
        // Idempotent finalize if user forgot to call Done()
        builder.Done();
        return services;
    }

    public static CovenServiceBuilder BuildCoven(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new CovenServiceBuilder(services);
    }
}

public sealed class CovenServiceBuilder
{
    private readonly IServiceCollection _services;
    private readonly MagikRegistry _registry = new();
    private bool finalized;

    internal CovenServiceBuilder(IServiceCollection services)
    {
        _services = services;
    }

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

    public CovenServiceBuilder UseSelectionStrategy(ISelectionStrategy strategy)
    {
        _registry.SetSelectionStrategy(strategy);
        return this;
    }

    public CovenServiceBuilder MagikBlock<TIn, TOut>(Func<IServiceProvider, IMagikBlock<TIn, TOut>> factory, IEnumerable<string>? capabilities = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        EnsureNotFinalized();
        FactoryActivator activator = new(sp => factory(sp));
        object placeholder = new();
        _registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), placeholder, capabilities?.ToList(), null, activator));
        return this;
    }

    public CovenServiceBuilder LambdaBlock<TIn, TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        ArgumentNullException.ThrowIfNull(func);
        EnsureNotFinalized();
        MagikBlock<TIn, TOut> mb = new(func);
        _registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), mb, capabilities?.ToList(), typeof(MagikBlock<TIn, TOut>).Name, null));
        return this;
    }

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
