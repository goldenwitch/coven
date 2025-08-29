using Coven.Spellcasting.Agents.Codex;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class CodexCliAgentConstructionTests
{
    [Fact]
    public void Can_Construct_Agent_And_Read_Id()
    {
        var agent = new CodexCliAgent<string, string>(s => s, s => s);
        Assert.Equal("codex", agent.Id);
    }
}

