using System.Diagnostics;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

internal static class TailMuxTestHelpers
{
    internal static string NewTempFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"coven_mux_{Guid.NewGuid():N}.log");
        using (File.Create(path)) { }
        return path;
    }

    internal static async Task AppendLinesAsync(string path, IEnumerable<string> lines)
    {
        using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var writer = new StreamWriter(fs);
        foreach (var l in lines)
        {
            await writer.WriteLineAsync(l);
            await writer.FlushAsync();
        }
    }
}

