// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Covenants;
using Coven.FileSystem;
using Coven.FileSystem.Posix;
using Coven.Testing.Harness;
using Coven.Testing.Harness.Assertions;
using Xunit;

namespace Coven.E2E.Tests.Toys;

/// <summary>
/// E2E tests for the PosixFileReader toy application.
/// Validates that the FileSystem.Posix daemon reads files from a sandboxed root
/// and routes content (or errors) back through the console chat covenant.
/// </summary>
public sealed class PosixFileReaderTests : IDisposable
{
    private readonly string _tempDir;

    public PosixFileReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"coven-posix-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private E2ETestHost BuildHost()
    {
        return new E2ETestHostBuilder()
            .UseVirtualConsole()
            .ConfigureCoven(coven =>
            {
                ConsoleClientConfig config = new()
                {
                    InputSender = "console",
                    OutputSender = "reader"
                };

                BranchManifest chat = coven.UseConsoleChat(config);
                BranchManifest filesystem = coven.UsePosixFileSystem(_tempDir);

                coven.Covenant()
                    .Connect(chat)
                    .Connect(filesystem)
                    .Routes(c =>
                    {
                        c.Route<ChatAfferent, FileRead>(
                            (msg, ct) => Task.FromResult(
                                new FileRead(Guid.NewGuid().ToString(), msg.Text.Trim())));

                        c.Route<FileContent, ChatEfferent>(
                            (content, ct) => Task.FromResult(
                                new ChatEfferent("reader", content.Content)));

                        c.Route<FileFailure, ChatEfferent>(
                            (failure, ct) => Task.FromResult(
                                new ChatEfferent("reader", $"[{failure.FailureKind}] {failure.Message}")));
                    });
            })
            .Build();
    }

    private void WriteFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(_tempDir, relativePath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }

    // ── Happy-path tests ─────────────────────────────────────────────────

    /// <summary>
    /// Reading an existing file returns its content through the console output.
    /// </summary>
    [Fact]
    public async Task ReadExistingFileReturnsContent()
    {
        WriteFile("hello.txt", "Hello from POSIX!");

        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("hello.txt");

        string output = await host.Console.WaitForOutputContainingAsync(
            "Hello from POSIX!", TimeSpan.FromSeconds(5));

        Assert.Contains("Hello from POSIX!", output);
    }

    /// <summary>
    /// Reading a file in a subdirectory works with relative paths.
    /// </summary>
    [Fact]
    public async Task ReadFileInSubdirectory()
    {
        WriteFile(Path.Combine("sub", "nested.txt"), "Nested content");

        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("sub/nested.txt");

        string output = await host.Console.WaitForOutputContainingAsync(
            "Nested content", TimeSpan.FromSeconds(5));

        Assert.Contains("Nested content", output);
    }

    /// <summary>
    /// Multiple sequential reads each return the correct content.
    /// </summary>
    [Fact]
    public async Task MultipleSequentialReads()
    {
        WriteFile("a.txt", "Content A");
        WriteFile("b.txt", "Content B");

        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("a.txt");
        await host.Console.WaitForOutputContainingAsync("Content A", TimeSpan.FromSeconds(5));

        await host.Console.SendInputAsync("b.txt");
        await host.Console.WaitForOutputContainingAsync("Content B", TimeSpan.FromSeconds(5));
    }

    // ── Error-path tests ─────────────────────────────────────────────────

    /// <summary>
    /// Reading a non-existent file produces a NotFound failure message.
    /// </summary>
    [Fact]
    public async Task ReadMissingFileReturnsNotFound()
    {
        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("does-not-exist.txt");

        string output = await host.Console.WaitForOutputContainingAsync(
            "[NotFound]", TimeSpan.FromSeconds(5));

        Assert.Contains("[NotFound]", output);
    }

    // ── Journal tests ────────────────────────────────────────────────────

    /// <summary>
    /// A successful read records both FileRead and FileContent in the FileSystem journal.
    /// </summary>
    [Fact]
    public async Task JournalRecordsFileReadAndContent()
    {
        WriteFile("journal-test.txt", "journal data");

        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("journal-test.txt");
        await host.Console.WaitForOutputContainingAsync("journal data", TimeSpan.FromSeconds(5));

        IReadOnlyList<FileSystemEntry> entries =
            await host.Journals.GetEntriesAsync<FileSystemEntry>();

        Assert.Contains(entries, e => e is FileRead { Path: "journal-test.txt" });
        Assert.Contains(entries, e => e is FileContent { Content: "journal data" });
    }

    /// <summary>
    /// A failed read records FileRead and FileFailure in the journal.
    /// </summary>
    [Fact]
    public async Task JournalRecordsFileReadAndFailure()
    {
        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("ghost.txt");
        await host.Console.WaitForOutputContainingAsync("[NotFound]", TimeSpan.FromSeconds(5));

        IReadOnlyList<FileSystemEntry> entries =
            await host.Journals.GetEntriesAsync<FileSystemEntry>();

        Assert.Contains(entries, e => e is FileRead { Path: "ghost.txt" });
        Assert.Contains(entries, e => e is FileFailure { FailureKind: "NotFound" });
    }

    /// <summary>
    /// Correlation IDs match between request and response in the journal.
    /// </summary>
    [Fact]
    public async Task CorrelationIdsMatchBetweenRequestAndResponse()
    {
        WriteFile("corr.txt", "correlated");

        await using E2ETestHost host = BuildHost();
        await host.StartAsync();

        await host.Console.SendInputAsync("corr.txt");
        await host.Console.WaitForOutputContainingAsync("correlated", TimeSpan.FromSeconds(5));

        IReadOnlyList<FileSystemEntry> entries =
            await host.Journals.GetEntriesAsync<FileSystemEntry>();

        FileRead? read = entries.OfType<FileRead>().FirstOrDefault(r => r.Path == "corr.txt");
        FileContent? content = entries.OfType<FileContent>().FirstOrDefault(c => c.Content == "correlated");

        Assert.NotNull(read);
        Assert.NotNull(content);
        Assert.Equal(read.CorrelationId, content.CorrelationId);
    }
}
