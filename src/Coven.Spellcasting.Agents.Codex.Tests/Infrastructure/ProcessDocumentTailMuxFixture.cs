using Coven.Spellcasting.Agents.Codex;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class ProcessDocumentTailMuxFixture : ITailMuxFixture
{
    public ITestTailMux CreateMux(MuxArgs args)
    {
        var services = new ServiceCollection();

        services.AddTransient<ITailMux>(sp =>
        {
            if (OperatingSystem.IsWindows())
            {
                return new ProcessDocumentTailMux(
                    documentPath: args.DocumentPath,
                    fileName: "cmd.exe",
                    arguments: "/C more",
                    workingDirectory: Path.GetTempPath());
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return new ProcessDocumentTailMux(
                    documentPath: args.DocumentPath,
                    fileName: "/bin/sh",
                    arguments: "-c cat",
                    workingDirectory: Path.GetTempPath());
            }
            throw new NotSupportedException("Unsupported OS for test environment.");
        });

        using var sp = services.BuildServiceProvider();
        return new MuxAdapter(sp.GetRequiredService<ITailMux>());
    }

    public Task StimulateIncomingAsync(ITestTailMux mux, MuxArgs args, IEnumerable<string> lines)
        => TailMuxTestHelpers.AppendLinesAsync(args.DocumentPath, lines);
}
