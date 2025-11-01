// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Reasoning effort configuration for models that support reasoning.
/// This is mapped internally to the OpenAI SDK's effort levels.
/// </summary>
public enum ReasoningEffort
{
    Low,
    Medium,
    High
}

