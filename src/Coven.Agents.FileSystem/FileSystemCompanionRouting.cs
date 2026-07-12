// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;

namespace Coven.Agents.FileSystem;

internal static class FileSystemCompanionRouting
{
    internal const string Sender = "filesystem";
    internal const string ReadFileToolName = "read_file";

    internal static bool IsValidReadFileCall(AgentToolCall call)
        => string.Equals(call.ToolName, ReadFileToolName, StringComparison.Ordinal)
            && TryExtractPath(call.ArgumentsJson, out _, out _);

    internal static bool IsInvalidReadFileCall(AgentToolCall call)
        => string.Equals(call.ToolName, ReadFileToolName, StringComparison.Ordinal)
            && !TryExtractPath(call.ArgumentsJson, out _, out _);

    internal static string BuildInvalidReadFileMessage(AgentToolCall call)
    {
        _ = TryExtractPath(call.ArgumentsJson, out _, out string error);
        return $"Tool '{call.ToolName}' call '{call.CorrelationId}' is invalid: {error}";
    }

    internal static bool TryExtractPath(string argumentsJson, out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(argumentsJson);

            if (!doc.RootElement.TryGetProperty("path", out JsonElement pathElement))
            {
                error = "missing required string argument 'path'.";
                return false;
            }

            if (pathElement.ValueKind != JsonValueKind.String)
            {
                error = "argument 'path' must be a string.";
                return false;
            }

            string? value = pathElement.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "argument 'path' must be a non-empty string.";
                return false;
            }

            path = value;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"arguments JSON is invalid: {ex.Message}";
            return false;
        }
    }
}