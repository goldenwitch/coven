// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Daemonology;
using Coven.Gemini.Client;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Agents.Gemini;

/// <summary>
/// Dependency Injection helpers for wiring the Gemini agent integration.
/// Registers journals, gateway connection, imbuing transmuters (position-based ACKs), windowing daemons, and REST gateway.
/// </summary>
public static class GeminiAgentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Gemini agents with required defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Gemini client configuration (API key and model are required).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddGeminiAgents(this IServiceCollection services, GeminiClientConfig config)
        => AddGeminiAgents(services, config, null);

    /// <summary>
    /// Registers Gemini agents with optional configuration of streaming/windowing behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Gemini client configuration (API key and model are required).</param>
    /// <param name="configure">Optional registration customization (e.g., enable streaming).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddGeminiAgents(this IServiceCollection services, GeminiClientConfig config, Action<GeminiRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new ArgumentException("GeminiClientConfig.ApiKey is required.");
        }
        if (string.IsNullOrWhiteSpace(config.Model))
        {
            throw new ArgumentException("GeminiClientConfig.Model is required.");
        }

        services.AddScoped(_ => config);

        GeminiRegistration registration = new();
        configure?.Invoke(registration);

        services.TryAddScoped<IScrivener<AgentEntry>, InMemoryScrivener<AgentEntry>>();
        services.AddKeyedScoped<IScrivener<GeminiEntry>, InMemoryScrivener<GeminiEntry>>("Coven.InternalGeminiScrivener");
        services.AddScoped<IScrivener<GeminiEntry>, GeminiScrivener>();
        if (registration.StreamingEnabled)
        {
            services.TryAddScoped<IGeminiGatewayConnection, GeminiStreamingGatewayConnection>();
        }
        else
        {
            services.TryAddScoped<IGeminiGatewayConnection, GeminiRequestGatewayConnection>();
        }

        services.AddScoped<IImbuingTransmuter<GeminiEntry, long, AgentEntry>, GeminiTransmuter>();
        services.AddScoped<IImbuingTransmuter<AgentEntry, long, GeminiEntry>, GeminiTransmuter>();
        services.TryAddScoped<ITransmuter<GeminiEntry, GeminiContent?>, GeminiEntryToContentTransmuter>();
        services.TryAddScoped<IGeminiTranscriptBuilder, GeminiTranscriptBuilder>();
        services.TryAddScoped<ITransmuter<GeminiClientConfig, GeminiRequestOptions>, GeminiResponseOptionsTransmuter>();
        services.TryAddScoped<IShatterPolicy<GeminiEntry>>(_ => new GeminiReasoningParagraphShatterPolicy());
        services.TryAddScoped<IShatterPolicy<AgentEntry>>(_ => new AgentThoughtSummaryShatterPolicy());

        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<GeminiAgentSessionFactory>();
        services.AddScoped<ContractDaemon, GeminiAgentDaemon>();

        if (registration.StreamingEnabled)
        {
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

        services.TryAddScoped<IBatchTransmuter<AgentAfferentChunk, AgentResponse>, AgentAfferentBatchTransmuter>();
        services.TryAddScoped<IBatchTransmuter<AgentAfferentThoughtChunk, AgentThought>, AgentAfferentThoughtBatchTransmuter>();
        return services;
    }
}
