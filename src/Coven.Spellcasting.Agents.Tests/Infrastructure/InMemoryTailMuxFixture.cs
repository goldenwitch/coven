// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Coven.Spellcasting.Agents.Tests.Infrastructure;

/// <summary>
/// Fixture that provides an in-memory ITailMux. It does not use a filesystem-backed source;
/// instead, <see cref="StimulateIncomingAsync"/> directly feeds <see cref="Line"/> events
/// into the mux. <see cref="CreateBackingFileAsync"/> is a no-op.
/// </summary>
public sealed class InMemoryTailMuxFixture : ITailMuxFixture, IDisposable
{
    private readonly IHost _host;

    public InMemoryTailMuxFixture()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddTransient<ITailMux>(sp => new InMemoryTailMux());
            })
            .Build();
    }

    /// <summary>
    /// Create a new in-memory mux instance and adapt it for the contract tests.
    /// </summary>
    public ITestTailMux CreateMux()
    {
        var inner = _host.Services.GetRequiredService<ITailMux>();
        return new MuxAdapter(inner);
    }

    /// <summary>
    /// Simulate incoming log lines by feeding Line events into the in-memory mux.
    /// </summary>
    public async Task StimulateIncomingAsync(ITestTailMux mux, IEnumerable<string> lines)
    {
        var im = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
        foreach (var l in lines)
            await im.FeedAsync(new Line(l, DateTimeOffset.UtcNow));
    }

    /// <summary>No backing file exists for in-memory muxes.</summary>
    public Task CreateBackingFileAsync(ITestTailMux mux) => Task.CompletedTask;

    public void Dispose()
    {
        _host.Dispose();
    }
}