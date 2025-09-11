// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Coven.Durables.Tests;

public class SimpleFileStorageTests : IDisposable
{
    private readonly string _path;

    public SimpleFileStorageTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "coven-durables-tests");
        Directory.CreateDirectory(tempDir);
        _path = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");
    }
    public record class TestRecord
    {
        public int A { get; init; }
        public string? B { get; init; }
        public TestRecord() { }
    }

    [Fact]
    public async Task SaveAndLoad_ListOfRecords_Works()
    {
        var storage = new SimpleFileStorage<TestRecord>(_path);

        var expected = new List<TestRecord> { new TestRecord { A = 42, B = "hello" } };

        await storage.Save(expected);

        var roundTripped = await storage.Load();
        Assert.Equal(expected, roundTripped);

        await storage.Append(new TestRecord());
        var afterAppend = await storage.Load();
        Assert.Equal(expected.Count + 1, afterAppend.Count);
        Assert.Equal(new TestRecord(), afterAppend[^1]);

    }

    [Fact]
    public async Task Load_Throws_When_FileMissing()
    {
        var storage = new SimpleFileStorage<TestRecord>(_path);
        if (File.Exists(_path)) File.Delete(_path);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await storage.Load());
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
    }
}