// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;

namespace Coven.Chat.Ui;

/// <summary>
/// CovenServiceBuilder extension methods for UI chat integration with declarative covenants.
/// </summary>
public static class UiChatCovenBuilderExtensions
{
    /// <summary>
    /// Adds UI chat integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">UI client configuration.</param>
    /// <returns>A manifest declaring what the UI branch produces and consumes.</returns>
    public static BranchManifest UseUiChat(this CovenServiceBuilder coven, UiChatClientConfig config)
        => UseUiChat(coven, config, null);

    /// <summary>
    /// Adds UI chat integration with optional streaming configuration and returns a manifest
    /// for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">UI client configuration.</param>
    /// <param name="configure">Optional callback to configure rendering behavior.</param>
    /// <returns>A manifest declaring what the UI branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The UI chat branch:</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="ChatAfferent"/> (messages submitted by the user)</description></item>
    /// <item><description>Consumes: <see cref="ChatEfferent"/> (finalized messages to render)</description></item>
    /// <item><description>Requires: <see cref="ContractDaemon"/> (UI chat daemon)</description></item>
    /// </list>
    /// <para>
    /// When streaming is enabled, the branch also consumes <see cref="ChatChunk"/> so tokens
    /// render as they arrive, ahead of the windowed <see cref="ChatEfferent"/>.
    /// </para>
    /// </remarks>
    public static BranchManifest UseUiChat(
        this CovenServiceBuilder coven,
        UiChatClientConfig config,
        Action<UiChatRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentNullException.ThrowIfNull(config);

        UiChatRegistration registration = new();
        configure?.Invoke(registration);

        coven.Services.AddUiChat(config);

        HashSet<Type> consumes = [typeof(ChatEfferent)];
        if (registration.StreamingEnabled)
        {
            consumes.Add(typeof(ChatChunk));
        }

        return new BranchManifest(
            Name: "UiChat",
            JournalEntryType: typeof(ChatEntry),
            Produces: new HashSet<Type> { typeof(ChatAfferent) },
            Consumes: consumes,
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
