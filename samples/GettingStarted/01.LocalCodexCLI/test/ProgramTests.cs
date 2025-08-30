using Xunit;

namespace Coven.Samples.LocalCodexCLI.Tests;

public class ProgramTests
{
    [Fact]
    public void SampleScaffoldExists()
    {
        // Simple smoke test: ensure Program type is discoverable
        var type = typeof(Coven.Samples.LocalCodexCLI.Program);
        Assert.NotNull(type);
    }
}
