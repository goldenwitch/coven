// SPDX-License-Identifier: BUSL-1.1

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Coven.Chat.Tests.TestTooling;

public static class Timeouts
{
    public const int Short = 200;
    public const int Medium = 500;
    public const int Long = 2000;
}

public static class AsyncAssert
{
    public static async Task<T> CompletesSoon<T>(Task<T> task, int ms = Timeouts.Medium)
        => (await Task.WhenAny(task, Task.Delay(ms))) == task
            ? await task
            : throw new Xunit.Sdk.XunitException($"Task did not complete within {ms}ms");

    public static async Task DoesNotCompleteSoon(Task task, int ms = Timeouts.Short)
        => Xunit.Assert.NotSame(task, await Task.WhenAny(task, Task.Delay(ms)));
}

public sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FileScrivenerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
    }
}

public static class TestPaths
{
    public static string RecordPath(string root, long pos)
        => System.IO.Path.Combine(root, pos.ToString("D20") + ".json"); // matches FileScrivener NameDigits
}

public static class RawJson
{
    public static void WriteRawJsonRecord(string root, long pos, string typeAssemblyQualifiedName, string payloadJson)
    {
        var json = $$"""{"pos":{{pos}},"type":"{{typeAssemblyQualifiedName}}","payload":{{payloadJson}}}""";
        File.WriteAllText(TestPaths.RecordPath(root, pos), json, Encoding.UTF8);
    }
}

public static class OS
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
