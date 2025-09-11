// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Di;
using Xunit;

namespace Coven.Core.Tests;

public sealed class HostBuilderSugarTests
{
    private sealed class StringToInt : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }

    [Fact]
    public async Task HostApplicationBuilder_BuildCoven_Registers_And_Runs_Coven()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            // Intentionally omit Done() to validate auto-finalize in BuildCoven(Action<>)
        });

        using var host = builder.Build();
        var coven = host.Services.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, int>("hello");
        Assert.Equal(5, result);
    }
}
