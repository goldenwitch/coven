// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.FileSystem;

namespace Coven.Agents.FileSystem;

/// <summary>
/// Extension methods for <see cref="ICovenant"/> to wire FileSystem companion routes.
/// </summary>
public static class FileSystemCompanionCovenantExtensions
{
    /// <summary>
    /// Routes agent tool calls to/from the FileSystem branch via companion transmuters.
    /// Registers: AgentToolCall → FileRead, FileContent → AgentToolResult, FileFailure → AgentToolFailure.
    /// </summary>
    /// <param name="covenant">The covenant route builder.</param>
    /// <returns>The same covenant for fluent chaining.</returns>
    public static ICovenant RouteFileSystemTools(this ICovenant covenant)
    {
        return covenant
            .Route<AgentToolCall, FileRead, AgentToolCallToFileRead>(FileSystemCompanionRouting.IsValidReadFileCall)
            .Route<AgentToolCall, AgentToolFailure, InvalidReadFileCallToAgentToolFailure>(FileSystemCompanionRouting.IsInvalidReadFileCall)
            .Route<FileContent, AgentToolResult, FileContentToAgentToolResult>()
            .Route<FileFailure, AgentToolFailure, FileFailureToAgentToolFailure>();
    }
}
