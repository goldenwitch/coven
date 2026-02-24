// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// CovenServiceBuilder extension methods for LLamaSharp agents integration with declarative covenants.
/// </summary>
public static class LLamaSharpCovenBuilderExtensions
{
    /// <summary>
    /// Adds LLamaSharp agents integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">LLamaSharp client configuration.</param>
    /// <returns>A manifest declaring what the LLamaSharp branch produces and consumes.</returns>
    public static BranchManifest UseLLamaSharpAgents(this CovenServiceBuilder coven, LLamaSharpClientConfig config)
        => UseLLamaSharpAgents(coven, config, null);

    /// <summary>
    /// Adds LLamaSharp agents integration with optional streaming configuration
    /// and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">LLamaSharp client configuration.</param>
    /// <param name="configure">Optional callback to configure streaming behavior.</param>
    /// <returns>A manifest declaring what the LLamaSharp branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The LLamaSharp agents branch (non-streaming):</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="AgentResponse"/></description></item>
    /// <item><description>Consumes: <see cref="AgentPrompt"/></description></item>
    /// <item><description>Requires: <see cref="ContractDaemon"/> (LLamaSharp agent daemon)</description></item>
    /// </list>
    /// <para>When streaming is enabled, also produces:</para>
    /// <list type="bullet">
    /// <item><description><see cref="AgentAfferentChunk"/> (response chunks)</description></item>
    /// </list>
    /// </remarks>
    public static BranchManifest UseLLamaSharpAgents(
        this CovenServiceBuilder coven,
        LLamaSharpClientConfig config,
        Action<LLamaSharpRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentNullException.ThrowIfNull(config);

        // Capture streaming state before registration
        LLamaSharpRegistration registration = new();
        configure?.Invoke(registration);

        // Register LLamaSharp services using existing extension
        coven.Services.AddLLamaSharpAgents(config, configure);

        // Build produces set based on streaming configuration
        HashSet<Type> produces = [typeof(AgentResponse)];
        if (registration.StreamingEnabled)
        {
            produces.Add(typeof(AgentAfferentChunk));
        }

        // Return manifest for covenant connection
        return new BranchManifest(
            Name: "LLamaSharpAgents",
            JournalEntryType: typeof(AgentEntry),
            Produces: produces,
            Consumes: new HashSet<Type> { typeof(AgentPrompt) },
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
