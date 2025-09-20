// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class CovenE2EAsyncTests
{
    [Fact]
    public async Task Ritual_Awaits_MultipleAsyncBlocks_EndToEnd()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int>(sp => new AsyncDelayThenLength(40));
            c.AddBlock<int, double>(sp => new AsyncDelayThenToDouble(40));
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var sw = Stopwatch.StartNew();
        var result = await coven.Ritual<string, double>("abcd");
        sw.Stop();

        Assert.Equal(4d, result);
        // Two ~40ms delays with scheduling slack
        Assert.True(sw.ElapsedMilliseconds >= 75, $"Ritual finished too quickly: {sw.ElapsedMilliseconds}ms");
    }

    private sealed class AsyncDelayThenLength : IMagikBlock<string, int>
    {
        private readonly int delayMs;
        public AsyncDelayThenLength(int delayMs) { this.delayMs = delayMs; }
        public async Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncDelayThenToDouble : IMagikBlock<int, double>
    {
        private readonly int delayMs;
        public AsyncDelayThenToDouble(int delayMs) { this.delayMs = delayMs; }
        public async Task<double> DoMagik(int input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            return (double)input;
        }
    }
}
