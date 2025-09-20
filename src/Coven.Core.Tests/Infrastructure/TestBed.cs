// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Di;

namespace Coven.Core.Tests.Infrastructure;

internal sealed class TestHost(ServiceProvider services) : IDisposable
{
    public ServiceProvider Services { get; } = services;
    public ICoven Coven => Services.GetRequiredService<ICoven>();

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
        ServiceCollection services = new();
        configureServices?.Invoke(services);
        services.BuildCoven(build); // auto-finalizes if Done() omitted
        ServiceProvider sp = services.BuildServiceProvider();
        return new TestHost(sp);
    }

    public static TestHost BuildPull(Action<CovenServiceBuilder> build, PullOptions? options = null)
        => BuildPull(build, options, null);

    public static TestHost BuildPull(Action<CovenServiceBuilder> build, PullOptions? options, Action<IServiceCollection>? configureServices)
    {
        ServiceCollection services = new();
        configureServices?.Invoke(services);
        services.BuildCoven(c =>
        {
            build(c);
            c.Done(pull: true, pullOptions: options);
        });
        ServiceProvider sp = services.BuildServiceProvider();
        return new TestHost(sp);
    }
}
