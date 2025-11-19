// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Chat.Console;

/// <summary>
/// Dependency Injection helpers for wiring the Console chat adapter.
/// Registers gateway/session components, journals, the Console↔Chat transmuter, and the console daemon.
/// </summary>
public static class ConsoleChatServiceCollectionExtensions
{
    /// <summary>
    /// Adds Console chat integration using the provided client configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Console client configuration (input/output sender labels).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddConsoleChat(this IServiceCollection services, ConsoleClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(sp => config);
        services.AddScoped<ConsoleGatewayConnection>();
        services.AddScoped<ConsoleChatSessionFactory>();

        // Default ChatEntry journal if none provided by host
        services.TryAddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        services.AddScoped<IScrivener<ConsoleEntry>, ConsoleScrivener>();
        services.AddKeyedScoped<IScrivener<ConsoleEntry>, InMemoryScrivener<ConsoleEntry>>("Coven.InternalConsoleScrivener");

        services.AddScoped<IBiDirectionalTransmuter<ConsoleEntry, ChatEntry>, ConsoleTransmuter>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<ContractDaemon, ConsoleChatDaemon>();
        return services;
    }
}
