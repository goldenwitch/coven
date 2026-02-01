// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Core.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Agents.Claude;

/// <summary>
/// Dependency Injection helpers for wiring the Claude agent integration.
/// Registers journals, gateway connection, imbuing transmuters (position-based ACKs), windowing daemons, and REST gateway.
/// </summary>
public static class ClaudeAgentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Claude agents with required defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Claude client configuration (API key and model are required).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddClaudeAgents(this IServiceCollection services, ClaudeClientConfig config)
        => AddClaudeAgents(services, config, null);

    /// <summary>
    /// Registers Claude agents with optional configuration of streaming/windowing behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Claude client configuration (API key and model are required).</param>
    /// <param name="configure">Optional registration customization (e.g., enable streaming).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddClaudeAgents(this IServiceCollection services, ClaudeClientConfig config, Action<ClaudeRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new ArgumentException("ClaudeClientConfig.ApiKey is required.");
        }
        if (string.IsNullOrWhiteSpace(config.Model))
        {
            throw new ArgumentException("ClaudeClientConfig.Model is required.");
        }

        services.AddScoped(_ => config);

        ClaudeRegistration registration = new();
        configure?.Invoke(registration);

        // Journals
        services.TryAddScoped<IScrivener<AgentEntry>, InMemoryScrivener<AgentEntry>>();
        services.AddKeyedScoped<IScrivener<ClaudeEntry>, InMemoryScrivener<ClaudeEntry>>("Coven.InternalClaudeScrivener");
        services.AddScoped<IScrivener<ClaudeEntry>, ClaudeScrivener>();

        // Gateway connection (streaming or request-based)
        if (registration.StreamingEnabled)
        {
            services.TryAddScoped<IClaudeGatewayConnection, ClaudeStreamingGatewayConnection>();
        }
        else
        {
            services.TryAddScoped<IClaudeGatewayConnection, ClaudeRequestGatewayConnection>();
        }

        // Transmuters
        services.AddScoped<IImbuingTransmuter<ClaudeEntry, long, AgentEntry>, ClaudeTransmuter>();
        services.AddScoped<IImbuingTransmuter<AgentEntry, long, ClaudeEntry>, ClaudeTransmuter>();
        services.TryAddScoped<ITransmuter<ClaudeEntry, ClaudeMessage>, ClaudeEntryToMessageTransmuter>();
        services.TryAddScoped<IClaudeTranscriptBuilder, ClaudeTranscriptBuilder>();
        services.TryAddScoped<ITransmuter<ClaudeClientConfig, ClaudeRequestOptions>, ClaudeResponseOptionsTransmuter>();

        // Shatter policies
        services.TryAddScoped<IShatterPolicy<ClaudeEntry>>(_ => new ClaudeThinkingParagraphShatterPolicy());
        services.TryAddScoped<IShatterPolicy<AgentEntry>>(_ => new AgentThoughtSummaryShatterPolicy());

        // Daemon infrastructure
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<ClaudeAgentSessionFactory>();
        services.AddScoped<ContractDaemon, ClaudeAgentDaemon>();

        // Streaming windowing daemons
        if (registration.StreamingEnabled)
        {
            // Default window policies that can be overridden by the host
            services.TryAddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
                new CompositeWindowPolicy<AgentAfferentChunk>(
                    new AgentParagraphWindowPolicy(),
                    new AgentMaxLengthWindowPolicy(4096)
                ));
            services.TryAddScoped<IWindowPolicy<AgentAfferentThoughtChunk>>(_ =>
                new CompositeWindowPolicy<AgentAfferentThoughtChunk>(
                    new AgentThoughtSummaryMarkerWindowPolicy(),
                    new AgentThoughtMaxLengthWindowPolicy(4096)
                ));

            // Windowing daemon for response chunks
            services.AddScoped<ContractDaemon>(sp =>
            {
                IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
                IScrivener<AgentEntry> agentJournal = sp.GetRequiredService<IScrivener<AgentEntry>>();

                IWindowPolicy<AgentAfferentChunk> policy = sp.GetRequiredService<IWindowPolicy<AgentAfferentChunk>>();
                IBatchTransmuter<AgentAfferentChunk, AgentResponse> batchTransmuter =
                    sp.GetRequiredService<IBatchTransmuter<AgentAfferentChunk, AgentResponse>>();
                IShatterPolicy<AgentEntry>? shatterPolicy = sp.GetService<IShatterPolicy<AgentEntry>>();

                return new StreamWindowingDaemon<AgentEntry, AgentAfferentChunk, AgentResponse, AgentStreamCompleted>(
                    daemonEvents, agentJournal, policy, batchTransmuter, shatterPolicy);
            });

            // Windowing daemon for thought chunks
            services.AddScoped<ContractDaemon>(sp =>
            {
                IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
                IScrivener<AgentEntry> agentJournal = sp.GetRequiredService<IScrivener<AgentEntry>>();

                IWindowPolicy<AgentAfferentThoughtChunk> policy = sp.GetRequiredService<IWindowPolicy<AgentAfferentThoughtChunk>>();
                IBatchTransmuter<AgentAfferentThoughtChunk, AgentThought> batchTransmuter =
                    sp.GetRequiredService<IBatchTransmuter<AgentAfferentThoughtChunk, AgentThought>>();
                IShatterPolicy<AgentEntry>? shatterPolicy = sp.GetService<IShatterPolicy<AgentEntry>>();

                return new StreamWindowingDaemon<AgentEntry, AgentAfferentThoughtChunk, AgentThought, AgentStreamCompleted>(
                    daemonEvents, agentJournal, policy, batchTransmuter, shatterPolicy);
            });
        }

        // Batch transmuters for windowing
        services.TryAddScoped<IBatchTransmuter<AgentAfferentChunk, AgentResponse>, AgentAfferentBatchTransmuter>();
        services.TryAddScoped<IBatchTransmuter<AgentAfferentThoughtChunk, AgentThought>, AgentAfferentThoughtBatchTransmuter>();

        return services;
    }
}
