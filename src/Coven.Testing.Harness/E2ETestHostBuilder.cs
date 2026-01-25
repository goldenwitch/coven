// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.OpenAI;
using Coven.Chat.Console;
using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Coven.Testing.Harness;

/// <summary>
/// Fluent builder for creating E2E test hosts with virtual gateways.
/// </summary>
public sealed class E2ETestHostBuilder
{
    private readonly HostApplicationBuilder _builder;
    private readonly List<Type> _inMemoryScrivenerTypes = [];
    private bool _useVirtualConsole;
    private bool _useVirtualOpenAI;
    private bool _useVirtualDiscord;
    private TimeSpan _startupTimeout = TimeSpan.FromSeconds(30);
    private TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(10);
    private Action<CovenServiceBuilder>? _covenConfiguration;
    private Action<IServiceCollection>? _servicesConfiguration;

    /// <summary>
    /// Creates a new E2ETestHostBuilder.
    /// </summary>
    public E2ETestHostBuilder()
    {
        _builder = Host.CreateApplicationBuilder();
    }

    /// <summary>
    /// Configures the test host to use a virtual console for stdin/stdout testing.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder UseVirtualConsole()
    {
        _useVirtualConsole = true;
        return this;
    }

    /// <summary>
    /// Configures the test host to use a virtual OpenAI gateway with scripted responses.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder UseVirtualOpenAI()
    {
        _useVirtualOpenAI = true;
        return this;
    }

    /// <summary>
    /// Configures the test host to use a virtual Discord gateway.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder UseVirtualDiscord()
    {
        _useVirtualDiscord = true;
        return this;
    }

    /// <summary>
    /// Configures the Coven runtime using the standard builder pattern.
    /// </summary>
    /// <param name="configure">Configuration action for the Coven builder.</param>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder ConfigureCoven(Action<CovenServiceBuilder> configure)
    {
        _covenConfiguration = configure;
        return this;
    }

    /// <summary>
    /// Configures additional services in the DI container.
    /// </summary>
    /// <param name="configure">Configuration action for the service collection.</param>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        _servicesConfiguration = configure;
        return this;
    }

    /// <summary>
    /// Replaces a file scrivener with an in-memory scrivener for the specified entry type.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder WithInMemoryScrivener<TEntry>() where TEntry : notnull
    {
        _inMemoryScrivenerTypes.Add(typeof(TEntry));
        return this;
    }

    /// <summary>
    /// Sets the timeout for host startup (waiting for daemons to reach Running status).
    /// </summary>
    /// <param name="timeout">The startup timeout.</param>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder WithStartupTimeout(TimeSpan timeout)
    {
        _startupTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the timeout for host shutdown.
    /// </summary>
    /// <param name="timeout">The shutdown timeout.</param>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder WithShutdownTimeout(TimeSpan timeout)
    {
        _shutdownTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets both startup and shutdown timeouts.
    /// </summary>
    /// <param name="timeout">The timeout for both operations.</param>
    /// <returns>This builder for chaining.</returns>
    public E2ETestHostBuilder WithTimeout(TimeSpan timeout)
    {
        _startupTimeout = timeout;
        _shutdownTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Builds the E2E test host with all configured virtual gateways.
    /// </summary>
    /// <returns>The configured test host.</returns>
    public E2ETestHost Build()
    {
        VirtualConsoleIO? virtualConsole = null;
        VirtualOpenAIGateway? virtualOpenAI = null;
        VirtualDiscordGateway? virtualDiscord = null;

        // Pre-create virtual gateways that need singleton semantics
        if (_useVirtualConsole)
        {
            virtualConsole = new VirtualConsoleIO();
        }

        if (_useVirtualDiscord)
        {
            virtualDiscord = new VirtualDiscordGateway();
        }

        // Configure Coven - this registers real implementations
        if (_covenConfiguration is not null)
        {
            _builder.Services.BuildCoven(_covenConfiguration);
        }

        // Replace real implementations with virtual ones AFTER BuildCoven
        // Use RemoveAll + Add pattern to ensure virtual implementations take precedence
        if (_useVirtualConsole)
        {
            _builder.Services.RemoveAll<IConsoleIO>();
            _builder.Services.AddSingleton<IConsoleIO>(virtualConsole!);
        }

        if (_useVirtualOpenAI)
        {
            // Create the gateway as a singleton. It uses AsyncLocal to access the
            // current daemon scope's service provider for scrivener resolution.
            // E2ETestHost.StartAsync sets the scope via VirtualOpenAIGateway.SetCurrentScope.
            virtualOpenAI = new VirtualOpenAIGateway();

            _builder.Services.RemoveAll<IOpenAIGatewayConnection>();
            _builder.Services.AddSingleton<IOpenAIGatewayConnection>(virtualOpenAI);
        }

        if (_useVirtualDiscord)
        {
            _builder.Services.RemoveAll<IDiscordGateway>();
            _builder.Services.AddSingleton<IDiscordGateway>(virtualDiscord!);
        }

        // Replace file scriveners with in-memory equivalents
        foreach (Type entryType in _inMemoryScrivenerTypes)
        {
            ReplaceFileScrivenerWithInMemory(_builder.Services, entryType);
        }

        // Apply additional service configuration
        _servicesConfiguration?.Invoke(_builder.Services);

        IHost host = _builder.Build();

        // virtualOpenAI is already set if UseVirtualOpenAI was called

        return new E2ETestHost(
            host,
            virtualConsole,
            virtualOpenAI,
            virtualDiscord,
            _startupTimeout,
            _shutdownTimeout);
    }

    private static void ReplaceFileScrivenerWithInMemory(IServiceCollection services, Type entryType)
    {
        Type scrivenerInterface = typeof(IScrivener<>).MakeGenericType(entryType);
        Type inMemoryType = typeof(InMemoryScrivener<>).MakeGenericType(entryType);

        // Remove existing registrations
        List<ServiceDescriptor> toRemove = [.. services.Where(d => d.ServiceType == scrivenerInterface)];
        foreach (ServiceDescriptor descriptor in toRemove)
        {
            services.Remove(descriptor);
        }

        // Add in-memory scrivener
        services.AddScoped(scrivenerInterface, inMemoryType);
    }
}
