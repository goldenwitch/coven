// SPDX-License-Identifier: BUSL-1.1

using Coven.FileSystem;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Agents.FileSystem;

/// <summary>
/// DI helpers for registering the FileSystem companion library (tool definitions and transmuters).
/// </summary>
public static class FileSystemCompanionServiceCollectionExtensions
{
    /// <summary>
    /// Registers FileSystem companion services: tool definitions and transmuters.
    /// </summary>
    public static IServiceCollection AddFileSystemCompanion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register tool definitions so agent leaves can discover them
        foreach (ToolDefinition tool in FileSystemTools.All)
        {
            services.AddSingleton(tool);
        }

        // Register transmuters for covenant routing
        services.TryAddScoped<ITransmuter<AgentToolCall, FileRead>, AgentToolCallToFileRead>();
        services.TryAddScoped<ITransmuter<FileContent, AgentToolResult>, FileContentToAgentToolResult>();
        services.TryAddScoped<ITransmuter<FileFailure, AgentToolFailure>, FileFailureToAgentToolFailure>();

        return services;
    }
}
