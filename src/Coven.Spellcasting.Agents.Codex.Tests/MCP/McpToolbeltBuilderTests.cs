using System.Text.Json;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.Tests.MCP;

public sealed class McpToolbeltBuilderTests
{
    [Fact]
    public void FromSpells_Maps_All_Fields()
    {
        var spells = new List<SpellDefinition>
        {
            new("build", "{\"type\":\"object\"}", "{\"type\":\"string\"}"),
            new("test",  "{\"type\":\"null\"}",  null)
        };

        var belt = Coven.Spellcasting.Agents.Codex.MCP.McpToolbeltBuilder.FromSpells(spells);

        Assert.Equal(2, belt.Tools.Count);
        Assert.Equal("build", belt.Tools[0].Name);
        Assert.Equal("test", belt.Tools[1].Name);
        Assert.Equal("{\"type\":\"object\"}", belt.Tools[0].InputSchema);
        Assert.Equal("{\"type\":\"string\"}", belt.Tools[0].OutputSchema);
        Assert.Equal("{\"type\":\"null\"}", belt.Tools[1].InputSchema);
        Assert.Null(belt.Tools[1].OutputSchema);
    }

    [Fact]
    public void ToJson_Emits_Tools_Array_With_Fields()
    {
        var spells = new List<SpellDefinition> { new("format", "{}", "{}") };
        var belt = Coven.Spellcasting.Agents.Codex.MCP.McpToolbeltBuilder.FromSpells(spells);

        var json = belt.ToJson();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("tools", out var tools));
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Equal(1, tools.GetArrayLength());
        var t0 = tools[0];
        Assert.Equal("format", t0.GetProperty("Name").GetString());
        Assert.Equal("{}", t0.GetProperty("InputSchema").GetString());
        Assert.Equal("{}", t0.GetProperty("OutputSchema").GetString());
    }
}

