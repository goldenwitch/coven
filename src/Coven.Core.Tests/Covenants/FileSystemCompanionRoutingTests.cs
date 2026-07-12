// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.FileSystem;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using FileContent = Coven.FileSystem.FileContent;
using FileFailure = Coven.FileSystem.FileFailure;
using FileRead = Coven.FileSystem.FileRead;
using FileSystemEntry = Coven.FileSystem.FileSystemEntry;

namespace Coven.Core.Tests.Covenants;

public class FileSystemCompanionRoutingTests
{
    private static BranchManifest AgentsManifest { get; } = new(
        "Agents",
        JournalEntryType: typeof(AgentEntry),
        Produces: new HashSet<Type> { typeof(AgentToolCall) },
        Consumes: new HashSet<Type> { typeof(AgentToolResult), typeof(AgentToolFailure) },
        RequiredDaemons: []);

    private static BranchManifest FileSystemManifest { get; } = new(
        "FileSystem",
        JournalEntryType: typeof(FileSystemEntry),
        Produces: new HashSet<Type> { typeof(FileContent), typeof(FileFailure) },
        Consumes: new HashSet<Type> { typeof(FileRead) },
        RequiredDaemons: []);

    [Fact]
    public void AddFileSystemCompanionIsIdempotentByToolName()
    {
        ServiceCollection services = new();

        services.AddFileSystemCompanion();
        services.AddFileSystemCompanion();

        ServiceProvider provider = services.BuildServiceProvider();

        try
        {
            ToolDefinition[] tools = [.. provider.GetServices<ToolDefinition>()];
            ToolDefinition readFile = Assert.Single(tools, tool => tool.Name == FileSystemTools.ReadFile.Name);
            Assert.Equal(FileSystemTools.ReadFile.Description, readFile.Description);
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public async Task RouteFileSystemToolsRoutesValidReadCallToFileRead()
    {
        using ServiceProvider provider = BuildProvider();
        DaemonScope scope = await CovenExecutionScope.BeginScopeAsync(provider, CancellationToken.None);

        try
        {
            InMemoryScrivener<AgentEntry> agentJournal = Assert.IsType<InMemoryScrivener<AgentEntry>>(
                scope.Scope.ServiceProvider.GetRequiredService<IScrivener<AgentEntry>>());
            InMemoryScrivener<FileSystemEntry> fileSystemJournal = Assert.IsType<InMemoryScrivener<FileSystemEntry>>(
                scope.Scope.ServiceProvider.GetRequiredService<IScrivener<FileSystemEntry>>());

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
            await agentJournal.WriteAsync(new AgentToolCall("tester", "read-1", FileSystemTools.ReadFile.Name, "{\"path\":\"README.md\"}"), cts.Token);

            (long _, FileRead entry) = await fileSystemJournal.WaitForAsync<FileRead>(
                0,
                read => read.CorrelationId == "read-1",
                cts.Token);

            Assert.Equal("README.md", entry.Path);
        }
        finally
        {
            await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
        }
    }

    [Fact]
    public async Task RouteFileSystemToolsRoutesInvalidReadCallToAgentToolFailure()
    {
        using ServiceProvider provider = BuildProvider();
        DaemonScope scope = await CovenExecutionScope.BeginScopeAsync(provider, CancellationToken.None);

        try
        {
            InMemoryScrivener<AgentEntry> agentJournal = Assert.IsType<InMemoryScrivener<AgentEntry>>(
                scope.Scope.ServiceProvider.GetRequiredService<IScrivener<AgentEntry>>());

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
            await agentJournal.WriteAsync(new AgentToolCall("tester", "bad-read", FileSystemTools.ReadFile.Name, "{}"), cts.Token);

            (long _, AgentToolFailure failure) = await agentJournal.WaitForAsync<AgentToolFailure>(
                0,
                entry => entry.CorrelationId == "bad-read",
                cts.Token);

            Assert.Equal("filesystem", failure.Sender);
            Assert.Contains(FileSystemTools.ReadFile.Name, failure.Error);
            Assert.Contains("bad-read", failure.Error);
            Assert.Contains("path", failure.Error);
        }
        finally
        {
            await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
        }
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddFileSystemCompanion();
        services.AddScoped<IScrivener<AgentEntry>>(_ => new InMemoryScrivener<AgentEntry>());
        services.AddScoped<IScrivener<FileSystemEntry>>(_ => new InMemoryScrivener<FileSystemEntry>());
        services.AddScoped<IScrivener<DaemonEvent>>(_ => new InMemoryScrivener<DaemonEvent>());

        services.BuildCoven(coven =>
        {
            coven.Covenant()
                .Connect(AgentsManifest)
                .Connect(FileSystemManifest)
                .Routes(c => c.RouteFileSystemTools());
        });

        return services.BuildServiceProvider();
    }
}