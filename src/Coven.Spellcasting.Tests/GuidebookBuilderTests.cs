// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed class GuidebookBuilderTests
{
    [Fact]
    public void Build_Composes_Text_File_And_Uri()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"coven_test_{Guid.NewGuid():N}.txt");
        File.WriteAllText(tmp, "FILETEXT");

        try
        {
            var gb = new GuidebookBuilder()
                .AddText("INTRO")
                .AddFile(tmp, alias: "LocalFile")
                .AddUri("Spec", "https://example.com/spec")
                .Build();

            var instructions = gb.BuildInstructions();
            Assert.Contains("INTRO", instructions);
            Assert.Contains("FILETEXT", instructions);

            Assert.True(gb.UriMap.ContainsKey("LocalFile"));
            Assert.True(gb.UriMap.ContainsKey("Spec"));

            var localUri = gb.UriMap["LocalFile"];
            Assert.StartsWith("file:", localUri, StringComparison.OrdinalIgnoreCase);
            var parsed = new Uri(localUri);
            Assert.Equal(Path.GetFullPath(tmp), parsed.LocalPath);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}