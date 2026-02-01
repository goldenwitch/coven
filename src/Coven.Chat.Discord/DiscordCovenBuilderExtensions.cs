// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;

namespace Coven.Chat.Discord;

/// <summary>
/// CovenServiceBuilder extension methods for Discord chat integration with declarative covenants.
/// </summary>
public static class DiscordCovenBuilderExtensions
{
    /// <summary>
    /// Adds Discord chat integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">Discord client configuration.</param>
    /// <returns>A manifest declaring what the Discord branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The Discord branch:</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="ChatAfferent"/> (incoming messages from Discord)</description></item>
    /// <item><description>Consumes: <see cref="ChatEfferent"/>, <see cref="ChatEfferentDraft"/>, <see cref="ChatChunk"/> (outgoing messages to Discord)</description></item>
    /// <item><description>Requires: <see cref="DiscordChatDaemon"/> (via ContractDaemon)</description></item>
    /// </list>
    /// </remarks>
    public static BranchManifest UseDiscordChat(this CovenServiceBuilder coven, DiscordClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentNullException.ThrowIfNull(config);

        // Register Discord services using existing extension
        coven.Services.AddDiscordChat(config);

        // Return manifest for covenant connection
        return new BranchManifest(
            Name: "DiscordChat",
            JournalEntryType: typeof(ChatEntry),
            Produces: new HashSet<Type> { typeof(ChatAfferent) },
            Consumes: new HashSet<Type> { typeof(ChatEfferent), typeof(ChatEfferentDraft), typeof(ChatChunk) },
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
