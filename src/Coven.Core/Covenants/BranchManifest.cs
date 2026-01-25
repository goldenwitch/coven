// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Declares what a branch (e.g., Discord chat, OpenAI agents) produces, consumes, and requires.
/// Used by the covenant builder for build-time validation and runtime dispatch.
/// </summary>
/// <param name="Name">Human-readable branch identifier (e.g., "DiscordChat", "OpenAIAgents").</param>
/// <param name="JournalEntryType">The base entry type for this branch's journal (e.g., typeof(ChatEntry)).</param>
/// <param name="Produces">Entry types this branch writes to its journal (outputs from the branch's perspective).</param>
/// <param name="Consumes">Entry types this branch reads from its journal and acts upon (inputs to the branch).</param>
/// <param name="RequiredDaemons">Daemon types that must be started for this branch to function.</param>
public sealed record BranchManifest(
    string Name,
    Type JournalEntryType,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons);
