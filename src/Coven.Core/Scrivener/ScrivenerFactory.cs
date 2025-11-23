// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Scrivener;

/// <summary>
/// Typed builder for creating an IScrivener for a specific entry type.
/// </summary>
public sealed class ScrivenerFactory<TEntry>(IServiceProvider serviceProvider) where TEntry : notnull
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private Type? _tapperType;
    private Type _innerType = typeof(InMemoryScrivener<TEntry>);
    private IScrivener<TEntry>? _tappedScrivener;

    /// <summary>
    /// Wrap the inner scrivener with a tapped scrivener type that will be activated via DI.
    /// </summary>
    /// <typeparam name="TTapper">The tapped scrivener type to wrap the inner instance.</typeparam>
    public ScrivenerFactory<TEntry> WithTap<TTapper>() where TTapper : TappedScrivener<TEntry>
    {
        _tapperType = typeof(TTapper);
        return this;
    }

    /// <summary>
    /// Wrap the inner scrivener with a tapped scrivener type that will be activated via DI.
    /// </summary>
    /// <param name="tappingScrivener">An individual instance to use as the tapping Scrivener.</param>
    /// <typeparam name="TTapper">The tapped scrivener type to wrap the inner instance.</typeparam>
    public ScrivenerFactory<TEntry> WithTap<TTapper>(TTapper tappingScrivener) where TTapper : TappedScrivener<TEntry>
    {
        _tappedScrivener = tappingScrivener;
        return this;
    }

    /// <summary>
    /// Define the type that will implement IScrivener directly.
    /// </summary>
    /// <typeparam name="TScrivener">The type of the scrivener to implement.</typeparam>
    /// <returns></returns>
    public ScrivenerFactory<TEntry> WithType<TScrivener>() where TScrivener : IScrivener<TEntry>
    {
        _innerType = typeof(TScrivener);
        return this;
    }

    /// <summary>
    /// Create the IScrivener instance based on the configured transport and options.
    /// When a tapper is specified, the tapper is created via DI and wraps the inner scrivener.
    /// </summary>
    public IScrivener<TEntry> Build()
    {
        using IDisposable _ = ScrivenerBuildReentrancy.Enter(typeof(TEntry), _innerType, _tapperType);
        // First, if we are provided a tapper instance just use that.
        if (_tappedScrivener != null)
        {
            return _tappedScrivener;
        }

        try
        {
            // Second, if only a tapper type is specified, use the instance from the sp, otherwise just produce the inner.
            IScrivener<TEntry> scrivener = (IScrivener<TEntry>)ActivatorUtilities.CreateInstance(_serviceProvider, _innerType);
            return _tapperType != null
                ? (IScrivener<TEntry>)ActivatorUtilities.CreateInstance(_serviceProvider, _tapperType, scrivener)
                : scrivener;
        }
        catch
        {
            throw;
        }
    }
}
