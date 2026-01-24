// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Agents;

/// <summary>
/// Covenant for agent message flows.
/// Guarantees connectivity from prompts through processing to responses and thoughts.
/// </summary>
public sealed class AgentCovenant : ICovenant
{
    /// <inheritdoc />
    public static string Name => "Agent";
}
