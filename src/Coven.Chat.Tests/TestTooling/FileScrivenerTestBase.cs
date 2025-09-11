// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Tests.TestTooling;

/// <summary>
/// Per-test file scrivener context: creates a unique temp directory and cleans it up on dispose.
/// Inherit this base in FileScrivener test classes and use <see cref="Create{T}()"/> to get an instance.
/// </summary>
public abstract class FileScrivenerTestBase : System.IDisposable
{
    private readonly TempDir _dir = new();

    protected string Root => _dir.Path;

    protected Coven.Chat.FileScrivener<T> Create<T>() where T : notnull
        => new(Root);

    public void Dispose() => _dir.Dispose();
}
