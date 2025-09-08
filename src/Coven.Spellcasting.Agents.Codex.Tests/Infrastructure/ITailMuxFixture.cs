using Microsoft.Extensions.DependencyInjection;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed record MuxArgs(string DocumentPath);

public interface ITailMuxFixture
{
    ITestTailMux CreateMux(MuxArgs args);
    Task StimulateIncomingAsync(ITestTailMux mux, MuxArgs args, IEnumerable<string> lines);
}
