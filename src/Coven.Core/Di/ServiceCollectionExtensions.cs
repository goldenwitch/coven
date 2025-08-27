using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tags;

namespace Coven.Core.Di;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection BuildCoven(this IServiceCollection services, Action<CovenServiceBuilder> build)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (build is null) throw new ArgumentNullException(nameof(build));

        var builder = new CovenServiceBuilder(services);
        build(builder);
        // Idempotent finalize if user forgot to call Done()
        builder.Done();
        return services;
    }

    public static CovenServiceBuilder BuildCoven(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        return new CovenServiceBuilder(services);
    }
}

public sealed class CovenServiceBuilder
{
    private readonly IServiceCollection services;
    private readonly List<MagikBlockDescriptor> registry = new();
    private bool finalized;

    internal CovenServiceBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    public CovenServiceBuilder AddBlock<TIn, TOut, TBlock>(ServiceLifetime lifetime = ServiceLifetime.Transient, IEnumerable<string>? capabilities = null)
        where TBlock : class, IMagikBlock<TIn, TOut>
    {
        EnsureNotFinalized();
        bool already = false;
        foreach (var sd in services)
        {
            if (sd.ServiceType == typeof(TBlock)) { already = true; break; }
        }
        if (!already)
        {
            services.Add(new ServiceDescriptor(typeof(TBlock), typeof(TBlock), lifetime));
        }
        // Merge capabilities from caller, attribute, and optional parameterless-ctor ITagCapabilities instance
        var mergedCaps = new List<string>();
        if (capabilities is not null) mergedCaps.AddRange(capabilities);
        var t = typeof(TBlock);
        var attr = (Tags.TagCapabilitiesAttribute?)Attribute.GetCustomAttribute(t, typeof(Tags.TagCapabilitiesAttribute));
        if (attr is not null && attr.Tags is not null)
        {
            mergedCaps.AddRange(attr.Tags);
        }
        if (typeof(ITagCapabilities).IsAssignableFrom(t))
        {
            var ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor is not null)
            {
                try
                {
                    var tmp = (ITagCapabilities?)Activator.CreateInstance(t);
                    if (tmp?.SupportedTags is not null) mergedCaps.AddRange(tmp.SupportedTags);
                }
                catch
                {
                    // Ignore failures; rely on attr or builder-provided caps
                }
            }
        }
        // Register with a DI activator; no proxy instance.
        var activator = new DiTypeActivator(typeof(TBlock));
        var placeholder = new object();
        registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), placeholder, mergedCaps, typeof(TBlock).Name, activator));
        return this;
    }

    public CovenServiceBuilder AddBlock<TIn, TOut>(Func<IServiceProvider, IMagikBlock<TIn, TOut>> factory, IEnumerable<string>? capabilities = null)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        EnsureNotFinalized();
        var activator = new FactoryActivator(sp => factory(sp));
        var placeholder = new object();
        registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), placeholder, capabilities?.ToList(), null, activator));
        return this;
    }

    public CovenServiceBuilder AddLambda<TIn, TOut>(Func<TIn, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));
        EnsureNotFinalized();
        var mb = new MagikBlock<TIn, TOut>(func);
        registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), mb, capabilities?.ToList(), typeof(MagikBlock<TIn, TOut>).Name, null));
        return this;
    }

    public IServiceCollection Done()
    {
        if (finalized) return services; // idempotent
        finalized = true;

        var board = new Board(Board.BoardMode.Push, registry);
        services.AddSingleton<IBoard>(_ => board);
        services.AddSingleton<ICoven>(sp => new Coven(board, sp));
        return services;
    }

    private void EnsureNotFinalized()
    {
        if (finalized) throw new InvalidOperationException("Cannot modify CovenServiceBuilder after Done().");
    }
}
