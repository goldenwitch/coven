using Coven.Core;
using Coven.Core.Streaming;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Chat;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatWindowing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure required journals exist
        services.TryAddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();
        services.TryAddSingleton<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();

        // Register generic windowing daemon for Chat using a DI-provided policy
        services.AddScoped<ContractDaemon>(sp =>
        {
            IScrivener<DaemonEvent> daemonEvents = sp.GetRequiredService<IScrivener<DaemonEvent>>();
            IScrivener<ChatEntry> chatJournal = sp.GetRequiredService<IScrivener<ChatEntry>>();

            // Prefer a DI-registered policy; fall back to final-only if none provided
            IWindowPolicy<ChatChunk> policy =
                sp.GetService<IWindowPolicy<ChatChunk>>() ?? new LambdaWindowPolicy<ChatChunk>(1, _ => false);
            ITransmuter<IEnumerable<ChatChunk>, BatchTransmuteResult<ChatChunk, ChatEfferent>> batchTransmuter =
                sp.GetRequiredService<ITransmuter<IEnumerable<ChatChunk>, BatchTransmuteResult<ChatChunk, ChatEfferent>>>();

            return new StreamWindowingDaemon<ChatEntry, ChatChunk, ChatEfferent, ChatStreamCompleted>(
                daemonEvents, chatJournal, policy, batchTransmuter);
        });

        services.TryAddScoped<ITransmuter<IEnumerable<ChatChunk>, BatchTransmuteResult<ChatChunk, ChatEfferent>>, ChatChunkBatchTransmuter>();

        return services;
    }
}
