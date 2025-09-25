// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Coven.Core.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Coven.Core.Tests;

public class BreadcrumbLoggingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task BreadcrumbsReflectCapabilityRoutingOrder()
    {
        InMemoryLoggerProvider provider = new();
        using TestHost host = TestBed.BuildPush(
            build: c =>
            {
                _ = c.MagikBlock<string, int, StringLengthBlock>(ServiceLifetime.Transient, capabilities: ["calc:length"])
                    .MagikBlock<string, int, StringHashBlock>(ServiceLifetime.Transient, capabilities: ["calc:hash"])
                    .MagikBlock<int, double, IntToDoubleBlock>(ServiceLifetime.Transient)
                    .Done();
            },
            configureServices: s => s.AddLogging(b => b.AddProvider(provider))
        );

        List<string> tags = ["calc:length"];
        double result = await host.Coven.Ritual<string, double>("abcd", tags);
        Assert.Equal(4d, result);

        // Snapshot log lines
        List<string> lines = [.. provider.Entries];
        // Find this ritual id from the begin line
        string? beginLine = lines.FirstOrDefault(l => l.Contains("Coven.Ritual", StringComparison.Ordinal) && l.Contains("Ritual begin rid=", StringComparison.Ordinal));
        Assert.NotNull(beginLine);
        int ridStart = beginLine!.IndexOf("rid=", StringComparison.Ordinal) + 4;
        int ridEnd = beginLine.IndexOf(':', ridStart);
        string rid = ridEnd > ridStart ? beginLine[ridStart..ridEnd] : beginLine[ridStart..];
        // Filter to our ritual id
        List<string> ritualLines = [.. lines.Where(l => l.Contains("Coven.Ritual", StringComparison.Ordinal) && l.Contains($"rid={rid}", StringComparison.Ordinal))];

        // Output ritual lines with ordering for diagnostics
        for (int i = 0; i < ritualLines.Count; i++)
        {
            output.WriteLine($"ritual[{i}] {ritualLines[i]}");
        }

        // Expect sequence: begin -> select StringLengthBlock -> complete StringLengthBlock -> select IntToDoubleBlock -> complete IntToDoubleBlock -> end
        int idxBegin = ritualLines.FindIndex(s => s.Contains("Ritual begin", StringComparison.Ordinal));
        int idxSelLen = ritualLines.FindIndex(s => s.Contains($"Select {nameof(StringLengthBlock)}", StringComparison.Ordinal));
        int idxCompLen = ritualLines.FindIndex(s => s.Contains($"Complete {nameof(StringLengthBlock)}", StringComparison.Ordinal));
        int idxSelCast = ritualLines.FindIndex(s => s.Contains($"Select {nameof(IntToDoubleBlock)}", StringComparison.Ordinal));
        int idxCompCast = ritualLines.FindIndex(s => s.Contains($"Complete {nameof(IntToDoubleBlock)}", StringComparison.Ordinal));
        int idxEnd = ritualLines.FindIndex(s => s.Contains("Ritual end", StringComparison.Ordinal));

        Assert.True(idxBegin >= 0, "Missing begin breadcrumb");
        Assert.True(idxSelLen > idxBegin, "LengthBlock select should follow begin");
        Assert.True(idxCompLen > idxSelLen, "LengthBlock complete should follow select");
        Assert.True(idxSelCast > idxCompLen, "IntToDouble select should follow LengthBlock complete");
        Assert.True(idxCompCast > idxSelCast, "IntToDouble complete should follow select");
        Assert.True(idxEnd > idxCompCast, "End breadcrumb should follow last completion");
    }
}
