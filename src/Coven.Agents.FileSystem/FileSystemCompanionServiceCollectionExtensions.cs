// SPDX-License-Identifier: BUSL-1.1

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
            if (services.Any(sd =>
                    sd.ServiceType == typeof(ToolDefinition) &&
                    sd.ImplementationInstance is ToolDefinition existing &&
                    string.Equals(existing.Name, tool.Name, StringComparison.Ordinal)))
            {
                continue;
            }

            services.AddSingleton(tool);
        }

        // Register transmuters for covenant routing
        services.TryAddScoped<AgentToolCallToFileRead>();
        services.TryAddScoped<InvalidReadFileCallToAgentToolFailure>();
        services.TryAddScoped<FileContentToAgentToolResult>();
        services.TryAddScoped<FileFailureToAgentToolFailure>();

        return services;
    }
}
