// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;
using Coven.Core.Streaming;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Chat;

/// <summary>
/// Adds generic chat windowing infrastructure (journal, daemon) with a DI-provided window policy.
/// Useful for chunking and emitting grouped <see cref="ChatChunk"/> as <see cref="ChatEfferent"/> entries.
/// </summary>
public static class ChatWindowingServiceCollectionExtensions
{
    /// <summary>
    /// Registers chat windowing components and a windowing daemon for <see cref="ChatEntry"/>.
    /// A custom <see cref="IWindowPolicy{TChunk}"/> can be supplied via DI; otherwise a final-only policy is used.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddChatWindowing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure required journals exist
        services.TryAddScoped(sp => sp.BuildScrivener<ChatEntry>().Build());
        services.TryAddSingleton(sp => sp.BuildScrivener<DaemonEvent>().Build());

        // Register generic windowing daemon for Chat using a DI-provided policy
        services.AddScoped<ContractDaemon>(sp =>
        {
            IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
            IScrivener<ChatEntry> chatJournal = sp.GetRequiredService<IScrivener<ChatEntry>>();

            // Prefer a DI-registered policy; fall back to final-only if none provided
            IWindowPolicy<ChatChunk> policy =
                sp.GetService<IWindowPolicy<ChatChunk>>() ?? new LambdaWindowPolicy<ChatChunk>(1, _ => false);
            IBatchTransmuter<ChatChunk, ChatEfferent> batchTransmuter =
                sp.GetRequiredService<IBatchTransmuter<ChatChunk, ChatEfferent>>();
            IShatterPolicy<ChatEntry>? shatterPolicy = sp.GetService<IShatterPolicy<ChatEntry>>();

            return new StreamWindowingDaemon<ChatEntry, ChatChunk, ChatEfferent, ChatStreamCompleted>(
                daemonEvents, chatJournal, policy, batchTransmuter, shatterPolicy);
        });

        services.TryAddScoped<IBatchTransmuter<ChatChunk, ChatEfferent>, ChatChunkBatchTransmuter>();

        return services;
    }
}
