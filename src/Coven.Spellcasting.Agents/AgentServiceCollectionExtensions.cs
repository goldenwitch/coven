// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core;

namespace Coven.Spellcasting.Agents;

public static class AgentServiceCollectionExtensions
{
    // Register a concrete agent type and map it as IAgentControl. Ensures AmbientAgent is configured.
    public static IServiceCollection AddCovenAgent<TMessage, TAgent>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAgent : class, ICovenAgent<TMessage>
        where TMessage : notnull
    {
        AmbientAgent.Configure(new DefaultAgentEnvironment());
        services.Add(new ServiceDescriptor(typeof(ICovenAgent<TMessage>), typeof(TAgent), lifetime));
        services.AddSingleton<IAgentControl>(sp => (IAgentControl)sp.GetRequiredService<ICovenAgent<TMessage>>());
        return services;
    }

    // Register via factory and map as IAgentControl. Ensures AmbientAgent is configured.
    public static IServiceCollection AddCovenAgent<TMessage>(
        this IServiceCollection services,
        Func<IServiceProvider, ICovenAgent<TMessage>> factory)
        where TMessage : notnull
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        AmbientAgent.Configure(new DefaultAgentEnvironment());
        services.AddSingleton<ICovenAgent<TMessage>>(sp => factory(sp));
        services.AddSingleton<IAgentControl>(sp => (IAgentControl)sp.GetRequiredService<ICovenAgent<TMessage>>());
        return services;
    }
}
