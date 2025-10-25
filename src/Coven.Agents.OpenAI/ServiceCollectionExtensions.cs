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

        // When streaming is enabled, include generic segmentation daemon bound to Agent types
        if (registration.StreamingEnabled)
        {
            services.AddScoped<ContractDaemon>(sp =>
            {
                IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
                IScrivener<AgentEntry> agentJournal = sp.GetRequiredService<IScrivener<AgentEntry>>();

                IStreamSegmenter<AgentChunk> segmenter = Segmenters.FinalOnly<AgentChunk>();
                LambdaTransmuter<AgentChunk, (string Sender, string Text)> chunkTransmuter = new((chunk, ct) =>
                    Task.FromResult((chunk.Sender, chunk.Text)));
                LambdaTransmuter<(string Sender, string Text), AgentResponse> outputTransmuter = new((input, ct) =>
                    Task.FromResult(new AgentResponse(input.Sender, input.Text)));

                return new StreamSegmentationDaemon<AgentEntry, AgentChunk, AgentResponse, AgentStreamCompleted>(
                    daemonEvents, agentJournal, segmenter, chunkTransmuter, outputTransmuter);
            });
        }
        return services;
    }
}
