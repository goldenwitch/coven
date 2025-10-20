// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Core;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Agents.OpenAI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAIAgents(this IServiceCollection services, OpenAIClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(sp => config);

        // Journals and gateway
        services.TryAddSingleton<IScrivener<AgentEntry>, InMemoryScrivener<AgentEntry>>();
        services.AddKeyedScoped<IScrivener<OpenAIEntry>, InMemoryScrivener<OpenAIEntry>>("Coven.InternalOpenAIScrivener");
        services.AddScoped<IScrivener<OpenAIEntry>, OpenAIScrivener>();
        services.AddScoped<OpenAIGatewayConnection>();

        // Transmuter and daemon
        services.AddScoped<IBiDirectionalTransmuter<OpenAIEntry, AgentEntry>, OpenAITransmuter>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<OpenAIAgentSessionFactory>();
        services.AddScoped<ContractDaemon, OpenAIAgentDaemon>();
        return services;
    }
}
