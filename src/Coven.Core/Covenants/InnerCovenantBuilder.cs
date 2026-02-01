// SPDX-License-Identifier: BUSL-1.1

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Covenants;

/// <summary>
/// Implementation of <see cref="IInnerCovenantBuilder"/> that builds inner covenants
/// for composite branches. Validates routes against boundary produces/consumes and
/// creates pumps that will run within the composite's child scope.
/// </summary>
internal sealed class InnerCovenantBuilder : IInnerCovenantBuilder
{
    /// <summary>
    /// The well-known name for the boundary manifest in inner covenants.
    /// </summary>
    internal const string BoundaryManifestName = "Boundary";

    private readonly Type _boundaryJournalType;
    private readonly IReadOnlySet<Type> _boundaryProduces;
    private readonly IReadOnlySet<Type> _boundaryConsumes;
    private readonly List<BranchManifest> _innerManifests = [];
    private bool _boundaryConnected;
    private bool _routesConfigured;

    /// <summary>
    /// Pumps built from the inner covenant routes.
    /// Available after <see cref="Routes"/> has been called.
    /// </summary>
    internal IReadOnlyList<PumpDescriptor> InnerPumps { get; private set; } = [];

    /// <summary>
    /// All inner branch manifests that were connected.
    /// </summary>
    internal IReadOnlyList<BranchManifest> InnerManifests => _innerManifests;

    internal InnerCovenantBuilder(
        Type boundaryJournalType,
        IReadOnlySet<Type> boundaryProduces,
        IReadOnlySet<Type> boundaryConsumes)
    {
        ArgumentNullException.ThrowIfNull(boundaryJournalType);
        ArgumentNullException.ThrowIfNull(boundaryProduces);
        ArgumentNullException.ThrowIfNull(boundaryConsumes);

        _boundaryJournalType = boundaryJournalType;
        _boundaryProduces = boundaryProduces;
        _boundaryConsumes = boundaryConsumes;
    }

    /// <inheritdoc />
    public BranchManifest Branch(
        string name,
        Type journalEntryType,
        IReadOnlySet<Type> produces,
        IReadOnlySet<Type> consumes,
        IReadOnlyList<Type> daemons)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(journalEntryType);
        ArgumentNullException.ThrowIfNull(produces);
        ArgumentNullException.ThrowIfNull(consumes);
        ArgumentNullException.ThrowIfNull(daemons);

        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Cannot declare branches after Routes() has been called.");
        }

        // Create and return the manifest—caller decides whether to Connect() it
        return new BranchManifest(name, journalEntryType, produces, consumes, daemons);
    }

    /// <inheritdoc />
    public IInnerCovenantBuilder Connect(BranchManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Cannot connect manifests after Routes() has been called.");
        }

        _innerManifests.Add(manifest);
        return this;
    }

    /// <inheritdoc />
    public IInnerCovenantBuilder ConnectBoundary()
    {
        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Cannot connect boundary after Routes() has been called.");
        }

        if (_boundaryConnected)
        {
            throw new InvalidOperationException(
                "ConnectBoundary() can only be called once.");
        }

        _boundaryConnected = true;

        // Create a synthetic manifest for the boundary journal
        BranchManifest boundaryManifest = new(
            BoundaryManifestName,
            _boundaryJournalType,
            _boundaryProduces,
            _boundaryConsumes,
            []);

        _innerManifests.Add(boundaryManifest);
        return this;
    }

    /// <inheritdoc />
    public void Routes(Action<ICovenant> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (_routesConfigured)
        {
            throw new InvalidOperationException(
                "Routes() can only be called once per inner covenant.");
        }
        _routesConfigured = true;

        // Collect route definitions
        CovenantDefinition definition = new();
        configure(definition);

        // Validate the inner covenant
        List<string> errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new CovenantValidationException(errors);
        }

        // Build entry-to-journal lookup from manifests
        // Group by entry type to detect duplicates across manifests
        List<IGrouping<Type, (Type Entry, Type Journal, string Manifest)>> entryMappings =
        [
            .. _innerManifests
                .SelectMany(m => m.Produces.Concat(m.Consumes)
                    .Select(t => (Entry: t, Journal: m.JournalEntryType, Manifest: m.Name)))
                .GroupBy(x => x.Entry)
        ];

        // Check for duplicate entry types across different journals
        List<string> duplicateErrors = [];
        foreach (IGrouping<Type, (Type Entry, Type Journal, string Manifest)> group in entryMappings)
        {
            Type[] distinctJournals = [.. group.Select(x => x.Journal).Distinct()];
            if (distinctJournals.Length > 1)
            {
                string manifests = string.Join(", ", group.Select(x => x.Manifest).Distinct());
                duplicateErrors.Add(
                    $"Entry type {group.Key.Name} appears in multiple journals: {manifests}. " +
                    $"Each entry type must belong to exactly one journal.");
            }
        }

        if (duplicateErrors.Count > 0)
        {
            throw new CovenantValidationException(duplicateErrors);
        }

        Dictionary<Type, Type> entryToJournal = entryMappings.ToDictionary(
            g => g.Key,
            g => g.First().Journal);

        // Build typed pumps for each route
        InnerPumps =
        [
            .. definition.Routes.Select(route => CreateTypedPump(route, entryToJournal))
        ];
    }

    private static readonly MethodInfo _buildPumpMethod =
        typeof(InnerCovenantBuilder).GetMethod(nameof(BuildPump), BindingFlags.NonPublic | BindingFlags.Static)!;

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
        // Note: The service provider passed at runtime will be the composite's child scope
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
        // Fully typed—no reflection, no dynamic dispatch
        IScrivener<TSourceJournal> source = sp.GetRequiredService<IScrivener<TSourceJournal>>();
        IScrivener<TTargetJournal> target = sp.GetRequiredService<IScrivener<TTargetJournal>>();

        await foreach ((_, TSourceJournal entry) in source.TailAsync(0, ct))
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
    /// Validates the inner covenant against connected manifests and boundary constraints.
    /// </summary>
    private List<string> Validate(CovenantDefinition definition)
    {
        List<string> errors = [];

        // Collect all produced and consumed types from inner manifests (excluding boundary)
        // The boundary has inverted semantics: its "produces" are route targets (outputs),
        // and its "consumes" are route sources (inputs from outer covenant).
        // Boundary validation is handled separately by Rules 4 and 5.
        HashSet<Type> allProduced = [];
        HashSet<Type> allConsumed = [];
        foreach (BranchManifest manifest in _innerManifests)
        {
            if (manifest.Name == BoundaryManifestName)
            {
                continue;
            }

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
        HashSet<Type> routeSources = [.. definition.Routes.Select(r => r.SourceType)];

        // Rule 1: Every type in any manifest's Produces has a Route or Terminal (coverage)
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

        // Rule 2: Each source type may have only one route (uniqueness)
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

        // Rule 3: Every type in any manifest's Consumes has a Route producing it (consumer satisfaction)
        foreach (Type consumed in allConsumed)
        {
            if (!routeTargets.Contains(consumed))
            {
                errors.Add(
                    $"{consumed.Name} is consumed but nothing routes to it.\n" +
                    $"  Add: c.Route<..., {consumed.Name}>()");
            }
        }

        // Rule 4: Every type in boundary.produces must be a route target or terminal
        // (boundary coherence - outbound). Terminal types are outputs that inner daemons
        // write directly to the boundary scrivener, bypassing routing.
        foreach (Type boundaryProduced in _boundaryProduces)
        {
            if (!routeTargets.Contains(boundaryProduced) && !terminalTypes.Contains(boundaryProduced))
            {
                errors.Add(
                    $"{boundaryProduced.Name} is in boundary.produces but no inner route targets it.");
            }
        }

        // Rule 5: Every type in boundary.consumes must be a route source (boundary coherence - inbound)
        foreach (Type boundaryConsumed in _boundaryConsumes)
        {
            if (!routeSources.Contains(boundaryConsumed))
            {
                errors.Add(
                    $"{boundaryConsumed.Name} is in boundary.consumes but no inner route dispatches it.");
            }
        }

        return errors;
    }
}
