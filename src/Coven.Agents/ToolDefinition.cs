// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents;

/// <summary>
/// Describes a tool that an agent can invoke — name, description, and an optional JSON input schema.
/// </summary>
public record ToolDefinition(string Name, string? Description = null, string? InputSchema = null);
