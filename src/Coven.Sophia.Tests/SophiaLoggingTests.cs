using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Coven.Durables;
using Coven.Sophia;
using Coven.Sophia.Tests.Helpers;

namespace Coven.Sophia.Tests;

public class SophiaLoggingTests : IDisposable
{
    private readonly string _path;
    private readonly IHost _host;

    public SophiaLoggingTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "coven-sophia-logging-tests");
        Directory.CreateDirectory(tempDir);
        _path = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IDurableList<string>>(_ => new TestLogStorage(_path));
                services.AddSophiaLogging(new SophiaLoggerOptions { MinimumLevel = LogLevel.Warning, Label = "test", IncludeScopes = true });
            })
            .Build();
    }

    [Fact]
    public async Task DI_WritesEntries_AndHonorsMinimumLevel()
    {
        var logger = _host.Services.GetRequiredService<ILogger<SophiaLoggingTests>>();

        var storage = (TestLogStorage)_host.Services.GetRequiredService<IDurableList<string>>();
        var warnTask = storage.WaitForContainsAsync("warn message");
        var errorTask = storage.WaitForContainsAsync("error message");

        logger.LogDebug("debug message");
        logger.LogInformation("info message");
        logger.LogWarning("warn message");
        logger.LogError("error message");

        // Wait until both expected entries are durably appended
        await Task.WhenAll(warnTask, errorTask);

        // Verify persisted content
        var entries = await storage.Load();

        Assert.DoesNotContain(entries, e => e.Contains("Debug", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e => e.Contains("Information", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, e => e.Contains("Warning", StringComparison.OrdinalIgnoreCase) && e.Contains("warn message", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Contains("Error", StringComparison.OrdinalIgnoreCase) && e.Contains("error message", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DI_IncludesScopes_WhenEnabled()
    {
        var logger = _host.Services.GetRequiredService<ILogger<SophiaLoggingTests>>();

        var storage = (TestLogStorage)_host.Services.GetRequiredService<IDurableList<string>>();
        var scopeTask = storage.WaitForContainsAsync("scoped warning");

        using (logger.BeginScope("scope-outer"))
        using (logger.BeginScope("scope-inner"))
        {
            logger.LogWarning("scoped warning");
        }

        await scopeTask;
        
        var entries = await storage.Load();
        var scoped = entries.LastOrDefault(e => e.Contains("scoped warning", StringComparison.Ordinal));
        Assert.NotNull(scoped);
        Assert.Contains("Scopes=[", scoped!);
        Assert.Contains("scope-outer", scoped!);
        Assert.Contains("scope-inner", scoped!);
    }

    [Fact]
    public void Provider_Dispose_BlocksCreateLogger()
    {
        // Directly validate provider disposal semantics
        var provider = new SophiaLoggerProvider(new SimpleFileStorage<string>(_path), new SophiaLoggerOptions());
        var logger = provider.CreateLogger("category");
        provider.Dispose();
        Assert.Throws<ObjectDisposedException>(() => provider.CreateLogger("again"));
    }

    public void Dispose()
    {
        try { _host.Dispose(); } catch { /* ignore */ }
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
    }
}
