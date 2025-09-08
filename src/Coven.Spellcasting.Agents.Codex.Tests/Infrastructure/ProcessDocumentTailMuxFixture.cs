using Coven.Spellcasting.Agents.Codex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class ProcessDocumentTailMuxFixture : ITailMuxFixture, IDisposable
{
    private readonly IHost _host;

    public ProcessDocumentTailMuxFixture()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddTransient<Func<MuxArgs, ITailMux>>(sp => args =>
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
            })
            .Build();
    }

    public ITestTailMux CreateMux(MuxArgs args)
    {
        var factory = _host.Services.GetRequiredService<Func<MuxArgs, ITailMux>>();
        return new MuxAdapter(factory(args));
    }

    public Task StimulateIncomingAsync(ITestTailMux mux, MuxArgs args, IEnumerable<string> lines)
        => TailMuxTestHelpers.AppendLinesAsync(args.DocumentPath, lines);

    public void Dispose()
    {
        _host.Dispose();
    }
}
