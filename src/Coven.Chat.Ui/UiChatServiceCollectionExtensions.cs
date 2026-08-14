// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Chat.Ui;

/// <summary>
/// Dependency Injection helpers for wiring the UI chat adapter.
/// Registers the channel, gateway/session components, journals, the UI↔Chat transmuter, and the UI daemon.
/// </summary>
public static class UiChatServiceCollectionExtensions
{
    /// <summary>
    /// Adds UI chat integration using the provided client configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">UI client configuration (input/output sender labels).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    /// <remarks>
    /// <see cref="IUiChannel"/> is registered as a <b>singleton</b> so a host can hold one
    /// reference across ritual scopes. A scoped channel would be replaced whenever the
    /// session is rebuilt, silently orphaning the user interface.
    /// </remarks>
    public static IServiceCollection AddUiChat(this IServiceCollection services, UiChatClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.TryAddSingleton<IUiChannel, UiChannel>();
        services.AddScoped(_ => config);
        services.AddScoped<UiChatGatewayConnection>();
        services.AddScoped<UiChatSessionFactory>();

        // Default ChatEntry journal if none provided by the host.
        services.TryAddScoped<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        services.AddScoped<IScrivener<UiChatEntry>, UiChatScrivener>();
        services.AddKeyedScoped<IScrivener<UiChatEntry>, InMemoryScrivener<UiChatEntry>>("Coven.InternalUiChatScrivener");

        // Imbuing transmuters (position-aware) for ack correctness.
        services.AddScoped<IImbuingTransmuter<UiChatEntry, long, ChatEntry>, UiChatTransmuter>();
        services.AddScoped<IImbuingTransmuter<ChatEntry, long, UiChatEntry>, UiChatTransmuter>();

        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<ContractDaemon, UiChatDaemon>();
        return services;
    }
}
