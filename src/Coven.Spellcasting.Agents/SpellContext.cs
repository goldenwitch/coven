namespace Coven.Spellcasting.Agents;

using System;
using System.Collections.Generic;

public interface ISpellContextFacet { }

public sealed record SpellContext
{
    public Uri? ContextUri { get; init; }
    public AgentPermissions? Permissions { get; init; }

    public IReadOnlyDictionary<Type, object> Facets { get; private init; }
        = new Dictionary<Type, object>();

    public SpellContext With<TFacet>(TFacet facet) where TFacet : class, ISpellContextFacet
    {
        var dict = new Dictionary<Type, object>(Facets) { [typeof(TFacet)] = facet };
        return this with { Facets = dict };
    }

    public TFacet? Get<TFacet>() where TFacet : class, ISpellContextFacet
        => Facets.TryGetValue(typeof(TFacet), out var o) ? (TFacet)o : null;

    public bool TryGet<TFacet>(out TFacet? facet) where TFacet : class, ISpellContextFacet
    {
        facet = Get<TFacet>();
        return facet is not null;
    }
}

