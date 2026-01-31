// SPDX-License-Identifier: BUSL-1.1

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Covenants;

/// <summary>
/// Implementation of <see cref="ICovenantBuilder"/> that collects manifests,
/// validates completeness, and registers the covenant dispatcher.
/// </summary>
internal sealed class CovenantBuilder : ICovenantBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<BranchManifest> _manifests = [];
    private readonly List<CompositeBranchManifest> _compositeManifests = [];
    private bool _routesConfigured;

    internal CovenantBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public ICovenantBuilder Connect(BranchManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Cannot connect manifests after Routes() has been called.");
        }
        _manifests.Add(manifest);
        return this;
    }

    /// <inheritdoc />
    public ICovenantBuilder Connect(CompositeBranchManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Cannot connect manifests after Routes() has been called.");
        }
        _compositeManifests.Add(manifest);

        // Convert to BranchManifest for outer covenant's perspective
        // (boundary produces/consumes, composite daemon as required daemon)
        BranchManifest boundaryView = new(
            manifest.Name,
            manifest.BoundaryJournalType,
            manifest.Produces,
            manifest.Consumes,
            [manifest.CompositeDaemonType]);

        _manifests.Add(boundaryView);
        return this;
    }

    /// <inheritdoc />
    public void Routes(Action<ICovenant> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Routes() can only be called once per covenant.");
        }
        _routesConfigured = true;

        // Collect route definitions
        CovenantDefinition definition = new();
        configure(definition);

        // Validate the covenant
        List<string> errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new CovenantValidationException(errors);
        }

        // Build entry-to-journal lookup from manifests
        Dictionary<Type, Type> entryToJournal = _manifests
            .SelectMany(m => m.Produces.Concat(m.Consumes)
                .Select(t => (Entry: t, Journal: m.JournalEntryType)))
            .ToDictionary(x => x.Entry, x => x.Journal);

        // Build typed pumps for each route (one-time reflection per route)
        List<PumpDescriptor> pumps =
        [
            .. definition.Routes.Select(route => CreateTypedPump(route, entryToJournal))
        ];

        // Create and register the covenant descriptor (scoped)
        CovenantDescriptor descriptor = new([.. _manifests], pumps);
        _services.AddScoped(_ => descriptor);

        // Register the covenant adherent daemon
        _services.AddScoped<IDaemon, CovenantAdherentDaemon>();

        // Then call existing RegisterDaemons() for branch daemons
        RegisterDaemons();
    }

    private static readonly MethodInfo _buildPumpMethod =
        typeof(CovenantBuilder).GetMethod(nameof(BuildPump), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static PumpDescriptor CreateTypedPump(
        RouteDescriptor route,
        Dictionary<Type, Type> entryToJournal)
    {
        Type sourceJournal = entryToJournal[route.SourceType];
        Type targetJournal = entryToJournal[route.TargetType];

        // One-time reflection to call BuildPump<TSourceJournal, TTargetJournal>
        MethodInfo generic = _buildPumpMethod.MakeGenericMethod(sourceJournal, targetJournal);
        return (PumpDescriptor)generic.Invoke(null, [route])!;
    }

    private static PumpDescriptor BuildPump<TSourceJournal, TTargetJournal>(RouteDescriptor route)
        where TSourceJournal : Entry
        where TTargetJournal : Entry
    {
        // Extract the invoker (handles both lambda and transmuter routes)
        Func<IServiceProvider, Func<Entry, CancellationToken, Task<Entry>>> getInvoker = route switch
        {
            LambdaRouteDescriptor lambda => _ => lambda.Invoke,
            TransmuterRouteDescriptor transmuter => transmuter.CreateInvoker,
            _ => throw new InvalidOperationException($"Unknown route type: {route.GetType().Name}")
        };

        // Return a pump factory with fully-typed scrivener access
        return new PumpDescriptor(
            route.SourceType,
            route.TargetType,
            (sp, ct) => RunPumpAsync<TSourceJournal, TTargetJournal>(
                sp, route.SourceType, getInvoker(sp), ct));
    }

    private static async Task RunPumpAsync<TSourceJournal, TTargetJournal>(
        IServiceProvider sp,
        Type sourceLeafType,
        Func<Entry, CancellationToken, Task<Entry>> invoke,
        CancellationToken ct)
        where TSourceJournal : Entry
        where TTargetJournal : Entry
    {
        IScrivener<TSourceJournal> source = sp.GetRequiredService<IScrivener<TSourceJournal>>();
        IScrivener<TTargetJournal> target = sp.GetRequiredService<IScrivener<TTargetJournal>>();

        await foreach ((long _, TSourceJournal entry) in source.TailAsync(0, ct))
        {
            if (entry.GetType() != sourceLeafType)
            {
                continue;
            }

            Entry result = await invoke(entry, ct);
            await target.WriteAsync((TTargetJournal)result, ct);
        }
    }

    /// <summary>
    /// Validates the covenant against connected manifests.
    /// </summary>
    private List<string> Validate(CovenantDefinition definition)
    {
        List<string> errors = [];

        // Collect all produced and consumed types
        HashSet<Type> allProduced = [];
        HashSet<Type> allConsumed = [];
        foreach (BranchManifest manifest in _manifests)
        {
            allProduced.UnionWith(manifest.Produces);
            allConsumed.UnionWith(manifest.Consumes);
        }

        // Build lookup structures
        Dictionary<Type, List<RouteDescriptor>> routesBySource = [];
        foreach (RouteDescriptor route in definition.Routes)
        {
            if (!routesBySource.TryGetValue(route.SourceType, out List<RouteDescriptor>? list))
            {
                list = [];
                routesBySource[route.SourceType] = list;
            }
            list.Add(route);
        }

        HashSet<Type> terminalTypes = [.. definition.Terminals.Select(t => t.SourceType)];
        HashSet<Type> routeTargets = [.. definition.Routes.Select(r => r.TargetType)];

        // Rule 1: Every type in any manifest's Produces has a Route or Terminal (no implicit ignoring)
        foreach (Type produced in allProduced)
        {
            bool hasRoute = routesBySource.ContainsKey(produced);
            bool isTerminal = terminalTypes.Contains(produced);

            if (!hasRoute && !isTerminal)
            {
                errors.Add(
                    $"{produced.Name} is produced but has no route and is not terminal.\n" +
                    $"  Add: c.Route<{produced.Name}, ...>() or c.Terminal<{produced.Name}>()");
            }
            else if (hasRoute && isTerminal)
            {
                errors.Add(
                    $"{produced.Name} has both a Route and a Terminal. Choose one.\n" +
                    $"  Remove: c.Terminal<{produced.Name}>() or c.Route<{produced.Name}, ...>()");
            }
        }

        // Rule 2: Each source type may have only one route
        foreach ((Type source, List<RouteDescriptor> routes) in routesBySource)
        {
            if (routes.Count > 1)
            {
                errors.Add(
                    $"{source.Name} has multiple routes. Each source type may have only one route.\n" +
                    $"  The covenant describes the canonical path. For side-effects, use scrivener\n" +
                    $"  decorators or imbuing transmuters.");
            }
        }

        // Rule 3: Every type in any manifest's Consumes has a Route producing it
        foreach (Type consumed in allConsumed)
        {
            if (!routeTargets.Contains(consumed))
            {
                errors.Add(
                    $"{consumed.Name} is consumed but nothing routes to it.\n" +
                    $"  Add: c.Route<..., {consumed.Name}>()");
            }
        }

        // Rule 4: Transmuter types must be registered in DI
        // (This is a warning-level check; we can't verify at build time without resolving)
        // The runtime will fail fast if a transmuter is not registered.
        foreach (RouteDescriptor route in definition.Routes)
        {
            if (route is TransmuterRouteDescriptor transmuter)
            {
                // Check if already registered
                bool isRegistered = false;
                foreach (ServiceDescriptor sd in _services)
                {
                    if (sd.ServiceType == transmuter.TransmuterType ||
                        sd.ImplementationType == transmuter.TransmuterType)
                    {
                        isRegistered = true;
                        break;
                    }
                }
                if (!isRegistered)
                {
                    errors.Add(
                        $"{transmuter.TransmuterType.Name} is not registered in the service container.\n" +
                        $"  Add: services.AddTransient<{transmuter.TransmuterType.Name}>()");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Registers daemon types from connected manifests as IDaemon services.
    /// </summary>
    /// <remarks>
    /// Branch registrations typically register their concrete daemon as a base type
    /// (e.g., <c>services.AddScoped&lt;ContractDaemon, ConsoleChatDaemon&gt;()</c>).
    /// This method forwards those registrations to <see cref="IDaemon"/> so the
    /// daemon scope can discover and start them.
    /// Multiple daemons may be registered under the same base type (e.g., multiple
    /// ContractDaemon registrations for chat, streaming, file scrivener, etc.).
    /// </remarks>
    private void RegisterDaemons()
    {
        // Collect required daemon base types from all manifests
        HashSet<Type> requiredDaemonTypes = [];
        foreach (BranchManifest manifest in _manifests)
        {
            foreach (Type daemonType in manifest.RequiredDaemons)
            {
                requiredDaemonTypes.Add(daemonType);
            }
        }

        // For each required daemon type, forward all its registrations to IDaemon
        foreach (Type daemonType in requiredDaemonTypes)
        {
            if (daemonType.IsAbstract || daemonType.IsInterface)
            {
                // Forward all registrations of this base type to IDaemon
                // Use GetServices to enumerate all registrations
                _services.AddScoped(typeof(IDaemon), sp =>
                {
                    // This creates a wrapper that returns the enumerable as individual services
                    // The actual resolution happens via the factory registration below
                    throw new InvalidOperationException(
                        "This placeholder should not be resolved directly. " +
                        "Use the enumerable registration pattern instead.");
                });

                // Remove the placeholder and add proper forwarding
                // We need to use a factory that resolves all instances
                ServiceDescriptor placeholderToRemove = _services.Last(
                    sd => sd.ServiceType == typeof(IDaemon));
                _services.Remove(placeholderToRemove);

                // Add a forwarding registration for each concrete registration
                foreach (ServiceDescriptor sd in _services.ToList())
                {
                    if (sd.ServiceType == daemonType)
                    {
                        if (sd.ImplementationType is not null)
                        {
                            _services.AddScoped(typeof(IDaemon), sd.ImplementationType);
                        }
                        else if (sd.ImplementationFactory is not null)
                        {
                            _services.AddScoped(typeof(IDaemon), sp =>
                                (IDaemon)sd.ImplementationFactory(sp));
                        }
                    }
                }
            }
            else
            {
                // Concrete type - register directly if not already present
                bool alreadyRegistered = false;
                foreach (ServiceDescriptor sd in _services)
                {
                    if (sd.ServiceType == typeof(IDaemon) &&
                        sd.ImplementationType == daemonType)
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (!alreadyRegistered)
                {
                    _services.AddScoped(typeof(IDaemon), daemonType);
                }
            }
        }
    }
}
