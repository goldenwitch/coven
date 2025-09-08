using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class ProcessDocumentTailMux_ContractTests : TailMuxContract<ProcessDocumentTailMuxFixture>
{
    public ProcessDocumentTailMux_ContractTests(ProcessDocumentTailMuxFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Write_Does_Not_Require_Tail()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        await using var mux = Fixture.CreateMux(new MuxArgs(doc));
        await mux.WriteLineAsync("hello world");
        await mux.WriteLineAsync("another line");
    }
}

