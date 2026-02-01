// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;

namespace Coven.Agents.OpenAI;

/// <summary>
/// CovenServiceBuilder extension methods for OpenAI agents integration with declarative covenants.
/// </summary>
public static class OpenAICovenBuilderExtensions
{
    /// <summary>
    /// Adds OpenAI agents integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">OpenAI client configuration.</param>
    /// <returns>A manifest declaring what the OpenAI branch produces and consumes.</returns>
    public static BranchManifest UseOpenAIAgents(this CovenServiceBuilder coven, OpenAIClientConfig config)
        => UseOpenAIAgents(coven, config, null);

    /// <summary>
    /// Adds OpenAI agents integration with optional streaming configuration
    /// and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">OpenAI client configuration.</param>
    /// <param name="configure">Optional callback to configure streaming behavior.</param>
    /// <returns>A manifest declaring what the OpenAI branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The OpenAI agents branch (non-streaming):</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="AgentResponse"/>, <see cref="AgentThought"/></description></item>
    /// <item><description>Consumes: <see cref="AgentPrompt"/></description></item>
    /// <item><description>Requires: <see cref="ContractDaemon"/> (OpenAI agent daemon)</description></item>
    /// </list>
    /// <para>When streaming is enabled, also produces:</para>
    /// <list type="bullet">
    /// <item><description><see cref="AgentAfferentChunk"/> (response chunks)</description></item>
    /// <item><description><see cref="AgentAfferentThoughtChunk"/> (thought chunks)</description></item>
    /// </list>
    /// </remarks>
    public static BranchManifest UseOpenAIAgents(
        this CovenServiceBuilder coven,
        OpenAIClientConfig config,
        Action<OpenAIRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentNullException.ThrowIfNull(config);

        // Capture streaming state before registration
        OpenAIRegistration registration = new();
        configure?.Invoke(registration);

        // Register OpenAI services using existing extension
        coven.Services.AddOpenAIAgents(config, configure);

        // Build produces set based on streaming configuration
        HashSet<Type> produces = [typeof(AgentResponse), typeof(AgentThought)];
        if (registration.StreamingEnabled)
        {
            produces.Add(typeof(AgentAfferentChunk));
            produces.Add(typeof(AgentAfferentThoughtChunk));
        }

        // Return manifest for covenant connection
        return new BranchManifest(
            Name: "OpenAIAgents",
            JournalEntryType: typeof(AgentEntry),
            Produces: produces,
            Consumes: new HashSet<Type> { typeof(AgentPrompt) },
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
