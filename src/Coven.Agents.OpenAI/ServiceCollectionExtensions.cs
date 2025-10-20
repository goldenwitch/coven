// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;
using System.ClientModel;

namespace Coven.Agents.OpenAI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAIAgents(this IServiceCollection services, OpenAIClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Validate required configuration early
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new ArgumentException("OpenAIClientConfig.ApiKey is required.");
        }
        if (string.IsNullOrWhiteSpace(config.Model))
        {
            throw new ArgumentException("OpenAIClientConfig.Model is required.");
        }

        services.AddScoped(_ => config);

        // OpenAI client (official SDK)
        services.AddScoped(sp =>
        {
            OpenAIClientConfig cfg = sp.GetRequiredService<OpenAIClientConfig>();
            OpenAIClientOptions options = new();
            if (!string.IsNullOrWhiteSpace(cfg.Organization))
            {
                options.OrganizationId = cfg.Organization;
            }


            if (!string.IsNullOrWhiteSpace(cfg.Project))
            {
                options.ProjectId = cfg.Project;
            }


            return new OpenAIClient(new ApiKeyCredential(cfg.ApiKey), options);
        });

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
