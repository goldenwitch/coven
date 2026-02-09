// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.FileSystem.Posix;

/// <summary>
/// CovenServiceBuilder extension methods for POSIX FileSystem integration.
/// </summary>
public static class PosixFileSystemExtensions
{
    /// <summary>
    /// Adds POSIX FileSystem integration and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="root">Root directory. All paths are resolved relative to this root.</param>
    /// <returns>A manifest declaring what the FileSystem branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The FileSystem branch:</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="FileContent"/>, <see cref="FileFailure"/></description></item>
    /// <item><description>Consumes: <see cref="FileRead"/> (file read commands)</description></item>
    /// <item><description>Requires: <see cref="ContractDaemon"/> (<see cref="PosixFileSystemDaemon"/>)</description></item>
    /// </list>
    /// </remarks>
    public static BranchManifest UsePosixFileSystem(this CovenServiceBuilder coven, string root)
    {
        ArgumentNullException.ThrowIfNull(coven);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        // Branch journal
        coven.Services.TryAddScoped<IScrivener<FileSystemEntry>, InMemoryScrivener<FileSystemEntry>>();

        // Leaf services
        PosixFileSystemConfig config = new() { Root = root };
        coven.Services.AddScoped(_ => config);
        coven.Services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        coven.Services.AddScoped<ContractDaemon, PosixFileSystemDaemon>();

        return new BranchManifest(
            Name: "FileSystem",
            JournalEntryType: typeof(FileSystemEntry),
            Produces: new HashSet<Type> { typeof(FileContent), typeof(FileFailure) },
            Consumes: new HashSet<Type> { typeof(FileRead) },
            RequiredDaemons: [typeof(ContractDaemon)]);
    }
}
