// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Core.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Dependency Injection helpers for wiring the LLamaSharp agent integration.
/// Registers journals, gateway connection, imbuing transmuters (position-based ACKs), windowing daemons, and local model gateway.
/// </summary>
public static class LLamaSharpAgentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers LLamaSharp agents with required defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">LLamaSharp client configuration (ModelPath is required).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddLLamaSharpAgents(this IServiceCollection services, LLamaSharpClientConfig config)
        => AddLLamaSharpAgents(services, config, null);

    /// <summary>
    /// Registers LLamaSharp agents with optional configuration of streaming/windowing behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">LLamaSharp client configuration (ModelPath is required).</param>
    /// <param name="configure">Optional registration customization (e.g., enable streaming).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddLLamaSharpAgents(this IServiceCollection services, LLamaSharpClientConfig config, Action<LLamaSharpRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(config.ModelPath))
        {
            throw new ArgumentException("LLamaSharpClientConfig.ModelPath is required.");
        }

        services.AddScoped(_ => config);

        LLamaSharpRegistration registration = new();
        configure?.Invoke(registration);

        // Journals
        services.TryAddScoped<IScrivener<AgentEntry>, InMemoryScrivener<AgentEntry>>();
        services.AddKeyedScoped<IScrivener<LLamaSharpEntry>, InMemoryScrivener<LLamaSharpEntry>>("Coven.InternalLLamaSharpScrivener");
        services.AddScoped<IScrivener<LLamaSharpEntry>, LLamaSharpScrivener>();

        // Gateway connection (streaming or request-based)
        if (registration.StreamingEnabled)
        {
            services.TryAddScoped<ILLamaSharpGatewayConnection, LLamaSharpStreamingGatewayConnection>();
        }
        else
        {
            services.TryAddScoped<ILLamaSharpGatewayConnection, LLamaSharpRequestGatewayConnection>();
        }

        // Transmuters
        services.AddScoped<IImbuingTransmuter<LLamaSharpEntry, long, AgentEntry>, LLamaSharpTransmuter>();
        services.AddScoped<IImbuingTransmuter<AgentEntry, long, LLamaSharpEntry>, LLamaSharpTransmuter>();
        services.TryAddScoped<ILLamaSharpTranscriptBuilder, LLamaSharpTranscriptBuilder>();

        // Daemon infrastructure
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<LLamaSharpAgentSessionFactory>();
        services.AddScoped<ContractDaemon, LLamaSharpAgentDaemon>();

        // Streaming windowing daemons
        if (registration.StreamingEnabled)
        {
            // Default window policies that can be overridden by the host
            services.TryAddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
                new CompositeWindowPolicy<AgentAfferentChunk>(
                    new AgentParagraphWindowPolicy(),
                    new AgentMaxLengthWindowPolicy(4096)
                ));

            // Windowing daemon for response chunks
            services.AddScoped<ContractDaemon>(sp =>
            {
                IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
                IScrivener<AgentEntry> agentJournal = sp.GetRequiredService<IScrivener<AgentEntry>>();

                IWindowPolicy<AgentAfferentChunk> policy = sp.GetRequiredService<IWindowPolicy<AgentAfferentChunk>>();
                IBatchTransmuter<AgentAfferentChunk, AgentResponse> batchTransmuter =
                    sp.GetRequiredService<IBatchTransmuter<AgentAfferentChunk, AgentResponse>>();

                return new StreamWindowingDaemon<AgentEntry, AgentAfferentChunk, AgentResponse, AgentStreamCompleted>(
                    daemonEvents, agentJournal, policy, batchTransmuter, shatterPolicy: null);
            });
        }

        // Batch transmuters for windowing
        services.TryAddScoped<IBatchTransmuter<AgentAfferentChunk, AgentResponse>, AgentAfferentBatchTransmuter>();

        return services;
    }
}
