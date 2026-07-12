// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.FileSystem;

/// <summary>
/// Tool definitions for the FileSystem branch. These are registered as ToolDefinitions
/// in DI so agent leaves can include them in LLM tool registration.
/// </summary>
public static class FileSystemTools
{
    /// <summary>Tool definition for reading a file.</summary>
    public static ToolDefinition ReadFile { get; } = new(
        Name: "read_file",
        Description: "Read the contents of a file at the specified path. Returns the file content as text.",
        InputSchema: """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The file path to read, relative to the workspace root."
                }
            },
            "required": ["path"]
        }
        """);

    /// <summary>All tool definitions provided by this companion.</summary>
    public static IReadOnlyList<ToolDefinition> All { get; } = [ReadFile];
}
