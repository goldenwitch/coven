// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Di;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public sealed class HostBuilderSugarTests
{
    [Fact]
    public async Task HostApplicationBuilderBuildCovenRegistersAndRunsCoven()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringLengthBlock>();
            // Intentionally omit Done() to validate auto-finalize in BuildCoven(Action<>)
        });

        using IHost host = builder.Build();
        ICoven coven = host.Services.GetRequiredService<ICoven>();
        Board board = Assert.IsType<Board>(host.Services.GetRequiredService<IBoard>());
        Assert.True(board.Status.CompiledPipelinesCount > 0);
        int result = await coven.Ritual<string, int>("hello");
        Assert.Equal(5, result);
    }
}
