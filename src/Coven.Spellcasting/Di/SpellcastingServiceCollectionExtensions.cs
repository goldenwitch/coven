namespace Coven.Spellcasting.Di;

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

public static class SpellcastingServiceCollectionExtensions
{
    public static IServiceCollection AddSpellcastingDefaults<TIn>(
        this IServiceCollection services,
        Action<DefaultBooksBuilder<TIn>> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var builder = new DefaultBooksBuilder<TIn>();
        configure(builder);
        builder.Apply(services);
        return services;
    }
}

public sealed class DefaultBooksBuilder<TIn>
{
    private Func<TIn, CancellationToken, Coven.Spellcasting.DefaultGuide>? _makeGuide;
    private Func<TIn, CancellationToken, Coven.Spellcasting.DefaultSpell>? _makeSpell;
    private Func<TIn, CancellationToken, Coven.Spellcasting.DefaultTest>?  _makeTest;

    public DefaultBooksBuilder<TIn> UseGuide<TDerived>(Func<TIn, CancellationToken, TDerived> make)
        where TDerived : Coven.Spellcasting.DefaultGuide
    {
        if (make is null) throw new ArgumentNullException(nameof(make));
        _makeGuide = (i, ct) => make(i, ct);
        return this;
    }

    public DefaultBooksBuilder<TIn> UseSpell<TDerived>(Func<TIn, CancellationToken, TDerived> make)
        where TDerived : Coven.Spellcasting.DefaultSpell
    {
        if (make is null) throw new ArgumentNullException(nameof(make));
        _makeSpell = (i, ct) => make(i, ct);
        return this;
    }

    public DefaultBooksBuilder<TIn> UseTest<TDerived>(Func<TIn, CancellationToken, TDerived> make)
        where TDerived : Coven.Spellcasting.DefaultTest
    {
        if (make is null) throw new ArgumentNullException(nameof(make));
        _makeTest = (i, ct) => make(i, ct);
        return this;
    }

    internal void Apply(IServiceCollection services)
    {
        if (_makeGuide is not null)
        {
            services.AddSingleton<Coven.Spellcasting.IGuidebookFactory<TIn, Coven.Spellcasting.DefaultGuide>>(
                sp => new Coven.Spellcasting.DelegateGuideFactory<TIn>(_makeGuide));
        }

        if (_makeSpell is not null)
        {
            services.AddSingleton<Coven.Spellcasting.ISpellbookFactory<TIn, Coven.Spellcasting.DefaultSpell>>(
                sp => new Coven.Spellcasting.DelegateSpellFactory<TIn>(_makeSpell));
        }

        if (_makeTest is not null)
        {
            services.AddSingleton<Coven.Spellcasting.ITestbookFactory<TIn, Coven.Spellcasting.DefaultTest>>(
                sp => new Coven.Spellcasting.DelegateTestFactory<TIn>(_makeTest));
        }
    }
}

