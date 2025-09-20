// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Di;

namespace Coven.Core.Tests.Infrastructure;

internal sealed class TestHost : IDisposable
{
    public ServiceProvider Services { get; }
    public ICoven Coven => Services.GetRequiredService<ICoven>();

    public TestHost(ServiceProvider services)
    {
        Services = services;
    }

    public void Dispose()
    {
        Services.Dispose();
    }
}

internal static class TestBed
{
    public static TestHost BuildPush(Action<CovenServiceBuilder> build)
    {
        return BuildPush(build, null);
    }

    public static TestHost BuildPush(Action<CovenServiceBuilder> build, Action<IServiceCollection>? configureServices)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        services.BuildCoven(build); // auto-finalizes if Done() omitted
        var sp = services.BuildServiceProvider();
        return new TestHost(sp);
    }

    public static TestHost BuildPull(Action<CovenServiceBuilder> build, PullOptions? options = null)
        => BuildPull(build, options, null);

    public static TestHost BuildPull(Action<CovenServiceBuilder> build, PullOptions? options, Action<IServiceCollection>? configureServices)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        services.BuildCoven(c =>
        {
            build(c);
            c.Done(pull: true, pullOptions: options);
        });
        var sp = services.BuildServiceProvider();
        return new TestHost(sp);
    }
}
