namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public interface ITailMuxFixture
{
    ITestTailMux CreateMux();
    Task StimulateIncomingAsync(ITestTailMux mux, IEnumerable<string> lines);
    Task CreateBackingFileAsync(ITestTailMux mux);
    Task WaitUntilTailReadyAsync(ITestTailMux mux, CancellationToken ct = default);
}
