// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Coven.Core.Tests;

public class AsyncLocalDebug(ITestOutputHelper output)
{
    [Fact]
    public async Task TestAsyncLocalFlow()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        output.WriteLine($"Before BeginScopeAsync: CurrentProvider is {(CovenExecutionScope.CurrentProvider is null ? "null" : "not null")}");

        DaemonScope scope = await CovenExecutionScope.BeginScopeAsync(sp, CancellationToken.None);

        // CRITICAL: Set from synchronous context AFTER awaiting
        CovenExecutionScope.SetCurrentScope(scope);

        output.WriteLine($"After SetCurrentScope: CurrentProvider is {(CovenExecutionScope.CurrentProvider is null ? "null" : "not null")}");

        Assert.NotNull(CovenExecutionScope.CurrentProvider);

        CovenExecutionScope.SetCurrentScope(null);
        await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
    }
}
