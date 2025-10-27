// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;
using System.ClientModel;
using OpenAI.Responses;
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAIAgents(this IServiceCollection services, OpenAIClientConfig config)
        => AddOpenAIAgents(services, config, null);

    public static IServiceCollection AddOpenAIAgents(this IServiceCollection services, OpenAIClientConfig config, Action<OpenAIRegistration>? configure)
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

        // Optional streaming configuration
        OpenAIRegistration registration = new();
        configure?.Invoke(registration);

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
        if (registration.StreamingEnabled)
        {
            services.TryAddScoped<IOpenAIGatewayConnection, OpenAIStreamingGatewayConnection>();
        }
        else
        {
            services.TryAddScoped<IOpenAIGatewayConnection, OpenAIRequestGatewayConnection>();
        }

        // Transmuter and daemon
        services.AddScoped<IBiDirectionalTransmuter<OpenAIEntry, AgentEntry>, OpenAITransmuter>();
        services.TryAddScoped<ITransmuter<OpenAIEntry, ResponseItem?>, OpenAIEntryToResponseItemTransmuter>();
        services.TryAddScoped<IOpenAITranscriptBuilder, DefaultOpenAITranscriptBuilder>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<OpenAIAgentSessionFactory>();
        services.AddScoped<ContractDaemon, OpenAIAgentDaemon>();

        // When streaming is enabled, include generic windowing daemons for OpenAI and Agent types
        if (registration.StreamingEnabled)
        {
            // Provide default window policies that can be overridden by the host.
            // Paragraph boundary first, then a safety cap to avoid unbounded buffers.
            services.TryAddScoped<IWindowPolicy<OpenAIAfferentChunk>>(_ =>
                new CompositeWindowPolicy<OpenAIAfferentChunk>(
                    new OpenAIParagraphWindowPolicy(),
                    new OpenAIMaxLengthWindowPolicy(4096)
                ));

            services.TryAddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
                new CompositeWindowPolicy<AgentAfferentChunk>(
                    new AgentParagraphWindowPolicy(),
                    new AgentMaxLengthWindowPolicy(4096)
                ));

            // OpenAI incoming chunk -> thought windowing (configurable policy)
            services.AddScoped<ContractDaemon>(sp =>
            {
                IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
                IScrivener<OpenAIEntry> openAIJournal = sp.GetRequiredService<IScrivener<OpenAIEntry>>();

                IWindowPolicy<OpenAIAfferentChunk> openAiPolicy = sp.GetRequiredService<IWindowPolicy<OpenAIAfferentChunk>>();
                ITransmuter<IEnumerable<OpenAIAfferentChunk>, BatchTransmuteResult<OpenAIAfferentChunk, OpenAIThought>> openAiBatchTransmuter =
                    sp.GetRequiredService<ITransmuter<IEnumerable<OpenAIAfferentChunk>, BatchTransmuteResult<OpenAIAfferentChunk, OpenAIThought>>>();

                return new StreamWindowingDaemon<OpenAIEntry, OpenAIAfferentChunk, OpenAIThought, OpenAIStreamCompleted>(
                    daemonEvents, openAIJournal, openAiPolicy, openAiBatchTransmuter);
            });

            services.AddScoped<ContractDaemon>(sp =>
            {
                IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
                IScrivener<AgentEntry> agentJournal = sp.GetRequiredService<IScrivener<AgentEntry>>();

                // Allow DI to provide a custom window policy via registration
                IWindowPolicy<AgentAfferentChunk> policy = sp.GetRequiredService<IWindowPolicy<AgentAfferentChunk>>();
                ITransmuter<IEnumerable<AgentAfferentChunk>, BatchTransmuteResult<AgentAfferentChunk, AgentResponse>> batchTransmuter =
                    sp.GetRequiredService<ITransmuter<IEnumerable<AgentAfferentChunk>, BatchTransmuteResult<AgentAfferentChunk, AgentResponse>>>();

                return new StreamWindowingDaemon<AgentEntry, AgentAfferentChunk, AgentResponse, AgentStreamCompleted>(
                    daemonEvents, agentJournal, policy, batchTransmuter);
            });
        }
        services.TryAddScoped<ITransmuter<IEnumerable<OpenAIAfferentChunk>, BatchTransmuteResult<OpenAIAfferentChunk, OpenAIThought>>, OpenAIChunkBatchTransmuter>();
        services.TryAddScoped<ITransmuter<IEnumerable<AgentAfferentChunk>, BatchTransmuteResult<AgentAfferentChunk, AgentResponse>>, AgentAfferentBatchTransmuter>();
        return services;
    }
}
