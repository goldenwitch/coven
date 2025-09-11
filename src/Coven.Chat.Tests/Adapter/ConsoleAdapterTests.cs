// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Tests.Adapter;

using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Chat.Adapter.Console;
using Coven.Chat.Tests.Adapter.TestTooling;
using Xunit;

public sealed class ConsoleAdapterTests : AdapterContractTests
{
    private readonly FakeConsoleIO _io = new();
    private readonly ConsoleAdapterOptions _opts = new() { InputSender = "user", EchoUserInput = false };

    protected override IAdapter<ChatEntry> CreateAdapter()
        => new ConsoleAdapter(_io, _opts);

    protected override Task ProduceInboundAsync(string text)
    {
        _io.EnqueueInput(text);
        return Task.CompletedTask;
    }

    protected override async Task<string?> TryConsumeOutboundAsync(TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (_io.TryDequeueOutput(out var line)) return line;
            await Task.Delay(10);
        }
        return null;
    }
}
