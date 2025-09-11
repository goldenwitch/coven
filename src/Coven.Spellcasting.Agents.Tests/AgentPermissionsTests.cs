// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class AgentPermissionsTests
{
    [Fact]
    public void Grants_And_Allows_Work()
    {
        var p = new AgentPermissions()
            .Grant<WriteFile>()
            .Grant<RunCommand>();

        Assert.True(p.Allows<WriteFile>());
        Assert.True(p.Allows<RunCommand>());
        Assert.False(p.Allows<NetworkAccess>());
    }

    [Fact]
    public void Presets_Work()
    {
        var none = AgentPermissions.None();
        Assert.False(none.Allows<WriteFile>());

        var edit = AgentPermissions.AutoEdit();
        Assert.True(edit.Allows<WriteFile>());
        Assert.False(edit.Allows<RunCommand>());

        var full = AgentPermissions.FullAuto();
        Assert.True(full.Allows<WriteFile>());
        Assert.True(full.Allows<RunCommand>());
    }
}
