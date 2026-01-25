// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Daemonology;

namespace Coven.Chat.Console;

/// <summary>
/// CovenServiceBuilder extension methods for Console chat integration with declarative covenants.
/// </summary>
public static class ConsoleCovenBuilderExtensions
{
    /// <summary>
    /// Adds Console chat integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">Console client configuration.</param>
    /// <returns>A manifest declaring what the Console branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The Console branch:</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="ChatAfferent"/> (incoming messages from console)</description></item>
    /// <item><description>Consumes: <see cref="ChatEfferent"/> (outgoing messages to console)</description></item>
    /// <item><description>Requires: <see cref="ConsoleChatDaemon"/> (via ContractDaemon)</description></item>
    /// </list>
    /// </remarks>
    public static BranchManifest UseConsoleChat(this CovenServiceBuilder coven, ConsoleClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentNullException.ThrowIfNull(config);

        // Register Console services using existing extension
        coven.Services.AddConsoleChat(config);

        // Return manifest for covenant connection
        return new BranchManifest(
            Name: "ConsoleChat",
            JournalEntryType: typeof(ChatEntry),
            Produces: new HashSet<Type> { typeof(ChatAfferent) },
            Consumes: new HashSet<Type> { typeof(ChatEfferent) },
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
