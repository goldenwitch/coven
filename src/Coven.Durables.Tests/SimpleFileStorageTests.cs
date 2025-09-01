using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Coven.Durables.Tests;

public class SimpleFileStorageTests
{
    public record class TestRecord
    {
        public int A { get; init; }
        public string? B { get; init; }
        public TestRecord() { }
    }

    [Fact]
    public async Task SaveAndLoad_ListOfRecords_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "coven-durables-tests");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");

        var storage = new SimpleFileStorage<TestRecord>(path);

        var initial = await storage.Load();
        Assert.NotNull(initial);
        Assert.Empty(initial);

        var expected = new List<TestRecord> { new TestRecord { A = 42, B = "hello" } };

        await storage.Save(expected);

        var roundTripped = await storage.Load();
        Assert.Equal(expected, roundTripped);

        await storage.Append(new TestRecord());
        var afterAppend = await storage.Load();
        Assert.Equal(expected.Count + 1, afterAppend.Count);
        Assert.Equal(new TestRecord(), afterAppend[^1]);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
