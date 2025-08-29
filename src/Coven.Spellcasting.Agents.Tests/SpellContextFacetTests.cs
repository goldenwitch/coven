using System;
using Coven.Spellcasting.Agents;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public sealed class GitFacet : ISpellContextFacet
{
    public string RepoRoot { get; }
    public string? Branch { get; }
    public GitFacet(string repoRoot, string? branch = null)
    { RepoRoot = repoRoot; Branch = branch; }
}

public class SpellContextFacetTests
{
    [Fact]
    public void With_Adds_And_Get_Retrieves_Facet()
    {
        var ctx = new SpellContext
        {
            ContextUri = new Uri("file:///tmp/repo"),
            Permissions = AgentPermissions.AutoEdit()
        };

        var ctx2 = ctx.With(new GitFacet("/tmp/repo", "main"));

        Assert.NotSame(ctx, ctx2);
        var facet = ctx2.Get<GitFacet>();
        Assert.NotNull(facet);
        Assert.Equal("/tmp/repo", facet!.RepoRoot);
        Assert.Equal("main", facet.Branch);
    }

    [Fact]
    public void TryGet_Returns_False_When_Absent()
    {
        var ctx = new SpellContext();
        Assert.False(ctx.TryGet<GitFacet>(out var gf));
        Assert.Null(gf);
    }
}

