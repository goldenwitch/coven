using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Coven.Core.Di;
using Xunit;
using Xunit.Abstractions;

namespace Coven.Core.Tests;

public class BreadcrumbLoggingTests
{
    private readonly ITestOutputHelper output;
    public BreadcrumbLoggingTests(ITestOutputHelper output) { this.output = output; }
    private sealed class LengthBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }
    private sealed class HashBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.GetHashCode());
    }
    private sealed class IntToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    [Fact]
    public async Task Breadcrumbs_Reflect_Capability_Routing_Order()
    {
        var provider = new Infrastructure.InMemoryLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(provider));
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, LengthBlock>(ServiceLifetime.Transient, capabilities: new[] { "calc:length" });
            c.AddBlock<string, int, HashBlock>(ServiceLifetime.Transient, capabilities: new[] { "calc:hash" });
            c.AddBlock<int, double, IntToDouble>(ServiceLifetime.Transient);
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var tags = new List<string> { "calc:length" };
        var result = await coven.Ritual<string, double>("abcd", tags);
        Assert.Equal(4d, result);

        // Snapshot log lines
        var lines = ((Infrastructure.InMemoryLoggerProvider)provider).Entries.ToList();
        // Find this ritual id from the begin line
        var beginLine = lines.FirstOrDefault(l => l.Contains("Coven.Ritual", StringComparison.Ordinal) && l.Contains("Ritual begin rid=", StringComparison.Ordinal));
        Assert.NotNull(beginLine);
        var ridStart = beginLine!.IndexOf("rid=", StringComparison.Ordinal) + 4;
        var ridEnd = beginLine.IndexOf(':', ridStart);
        var rid = ridEnd > ridStart ? beginLine.Substring(ridStart, ridEnd - ridStart) : beginLine.Substring(ridStart);
        // Filter to our ritual id
        var ritualLines = lines.Where(l => l.Contains("Coven.Ritual", StringComparison.Ordinal) && l.Contains($"rid={rid}", StringComparison.Ordinal)).ToList();

        // Output ritual lines with ordering for diagnostics
        for (int i = 0; i < ritualLines.Count; i++)
        {
            output.WriteLine($"ritual[{i}] {ritualLines[i]}");
        }

        // Expect sequence: begin -> select LengthBlock -> complete LengthBlock -> select IntToDouble -> complete IntToDouble -> end
        int idxBegin = ritualLines.FindIndex(s => s.Contains("Ritual begin", StringComparison.Ordinal));
        int idxSelLen = ritualLines.FindIndex(s => s.Contains("Select LengthBlock", StringComparison.Ordinal));
        int idxCompLen = ritualLines.FindIndex(s => s.Contains("Complete LengthBlock", StringComparison.Ordinal));
        int idxSelCast = ritualLines.FindIndex(s => s.Contains("Select IntToDouble", StringComparison.Ordinal));
        int idxCompCast = ritualLines.FindIndex(s => s.Contains("Complete IntToDouble", StringComparison.Ordinal));
        int idxEnd = ritualLines.FindIndex(s => s.Contains("Ritual end", StringComparison.Ordinal));

        Assert.True(idxBegin >= 0, "Missing begin breadcrumb");
        Assert.True(idxSelLen > idxBegin, "LengthBlock select should follow begin");
        Assert.True(idxCompLen > idxSelLen, "LengthBlock complete should follow select");
        Assert.True(idxSelCast > idxCompLen, "IntToDouble select should follow LengthBlock complete");
        Assert.True(idxCompCast > idxSelCast, "IntToDouble complete should follow select");
        Assert.True(idxEnd > idxCompCast, "End breadcrumb should follow last completion");
    }
}
