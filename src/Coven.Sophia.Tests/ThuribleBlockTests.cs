// SPDX-License-Identifier: BUSL-1.1

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Coven.Core;
using Coven.Core.Di;
using Coven.Durables;
using Coven.Sophia;

namespace Coven.Sophia.Tests;

public class ThuribleBlockTests : IDisposable
{
    private readonly string _path;
    private readonly IHost _host;

    public ThuribleBlockTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "coven-thurible-tests");
        Directory.CreateDirectory(tempDir);
        _path = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IDurableList<string>>(_ => new SimpleFileStorage<string>(_path));
                services.BuildCoven(c =>
                {
                    c.AddThurible<string, int>(
                        label: "smoke",
                        func: async (input, logs) =>
                        {
                            await logs.Append($"during-smoke-log : {input}");
                            return input.Length;
                        },
                        storageFactory: sp => sp.GetRequiredService<IDurableList<string>>()
                    );
                    c.Done();
                });
            })
            .Build();
    }

    [Fact]
    public async Task ThuribleBlock_EndToEnd_WritesAuditLogs_AndReturnsResult()
    {
        var coven = _host.Services.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, int>("hello");
        Assert.Equal(5, result);

        // Verify logs were written
        var storage = new SimpleFileStorage<string>(_path);
        var entries = await storage.Load();
        Assert.True(entries.Any(e => e.StartsWith("pre-smoke-log")), "Missing pre log");
        Assert.True(entries.Any(e => e.StartsWith("during-smoke-log")), "Missing during log");
        Assert.True(entries.Any(e => e.StartsWith("post-smoke-log")), "Missing post log");
    }

    public void Dispose()
    {
        try { _host.Dispose(); } catch { /* ignore */ }
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
    }
}