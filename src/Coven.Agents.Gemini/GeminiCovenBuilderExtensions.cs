// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;

namespace Coven.Agents.Gemini;

/// <summary>
/// CovenServiceBuilder extension methods for Gemini agents integration with declarative covenants.
/// </summary>
public static class GeminiCovenBuilderExtensions
{
    /// <summary>
    /// Adds Gemini agents integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">Gemini client configuration.</param>
    /// <returns>A manifest declaring what the Gemini branch produces and consumes.</returns>
    public static BranchManifest UseGeminiAgents(this CovenServiceBuilder coven, GeminiClientConfig config)
        => UseGeminiAgents(coven, config, null);

    /// <summary>
    /// Adds Gemini agents integration with optional streaming configuration
    /// and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="config">Gemini client configuration.</param>
    /// <param name="configure">Optional callback to configure streaming behavior.</param>
    /// <returns>A manifest declaring what the Gemini branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The Gemini agents branch (non-streaming):</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="AgentResponse"/>, <see cref="AgentThought"/></description></item>
    /// <item><description>Consumes: <see cref="AgentPrompt"/></description></item>
    /// <item><description>Requires: <see cref="ContractDaemon"/> (Gemini agent daemon)</description></item>
    /// </list>
    /// <para>When streaming is enabled, also produces:</para>
    /// <list type="bullet">
    /// <item><description><see cref="AgentAfferentChunk"/> (response chunks)</description></item>
    /// <item><description><see cref="AgentAfferentThoughtChunk"/> (thought chunks)</description></item>
    /// </list>
    /// </remarks>
    public static BranchManifest UseGeminiAgents(
        this CovenServiceBuilder coven,
        GeminiClientConfig config,
        Action<GeminiRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentNullException.ThrowIfNull(config);

        // Capture streaming state before registration
        GeminiRegistration registration = new();
        configure?.Invoke(registration);

        // Register Gemini services using existing extension
        coven.Services.AddGeminiAgents(config, configure);

        // Build produces set based on streaming configuration
        HashSet<Type> produces = [typeof(AgentResponse), typeof(AgentThought)];
        if (registration.StreamingEnabled)
        {
            produces.Add(typeof(AgentAfferentChunk));
            produces.Add(typeof(AgentAfferentThoughtChunk));
        }

        // Return manifest for covenant connection
        return new BranchManifest(
            Name: "GeminiAgents",
            JournalEntryType: typeof(AgentEntry),
            Produces: produces,
            Consumes: new HashSet<Type> { typeof(AgentPrompt) },
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
