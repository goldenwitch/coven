// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core;

/// <summary>
/// Fluent builder for constructing IScrivener instances with composable options.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ScrivenerBuilder"/>.
/// </remarks>
/// <param name="services">A required service provider used for activation and DI-backed taps.</param>
public sealed class ScrivenerBuilder(IServiceProvider services)
{
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));
    private ScrivenerTransport _transport = ScrivenerTransport.InMemory;


    /// <summary>
    /// Selects the underlying transport/storage mechanism for the scrivener.
    /// </summary>
    public ScrivenerBuilder WithTransport(ScrivenerTransport transport)
    {
        _transport = transport;
        return this;
    }

    /// <summary>
    /// Binds the builder to a specific entry type and returns a typed builder.
    /// </summary>
    public ScrivenerBuilder<TEntry> ForEntry<TEntry>() where TEntry : notnull
    {
        return new ScrivenerBuilder<TEntry>(_services, _transport);
    }
}

/// <summary>
/// Typed builder for creating an IScrivener for a specific entry type.
/// </summary>
public sealed class ScrivenerBuilder<TEntry> where TEntry : notnull
{
    private readonly IServiceProvider _services;
    private readonly ScrivenerTransport _transport;
    private Type? _tapperType;

    internal ScrivenerBuilder(IServiceProvider services, ScrivenerTransport transport)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _transport = transport;
    }

    /// <summary>
    /// Wrap the inner scrivener with a tapped scrivener type that will be activated via DI.
    /// The inner scrivener is constructed based on the builder's configured transport.
    /// </summary>
    /// <typeparam name="TTapper">The tapped scrivener type to wrap the inner instance.</typeparam>
    public ScrivenerBuilder<TEntry> WithTap<TTapper>() where TTapper : TappedScrivener<TEntry>
    {
        _tapperType = typeof(TTapper);
        return this;
    }

    /// <summary>
    /// Create the IScrivener instance based on the configured transport and options.
    /// When a tapper is specified, the tapper is created via DI and wraps the inner scrivener.
    /// </summary>
    public IScrivener<TEntry> Build()
    {
        IScrivener<TEntry> inner = _transport switch
        {
            ScrivenerTransport.InMemory => new InMemoryScrivener<TEntry>(),
            _ => throw new NotSupportedException($"Unsupported scrivener transport: {_transport}")
        };

        if (_tapperType is null)
        {
            return inner;
        }

        // Let DI provide additional dependencies for the tapper; pass the inner explicitly.
        object tapped = ActivatorUtilities.CreateInstance(_services, _tapperType, inner);
        return (IScrivener<TEntry>)tapped;
    }
}

/// <summary>
/// Transport/storage options for scriveners.
/// </summary>
public enum ScrivenerTransport
{
    /// <summary>
    /// Single-process, in-memory journal suitable for tests, toys, and ephemeral runtimes.
    /// </summary>
    InMemory = 0
}
