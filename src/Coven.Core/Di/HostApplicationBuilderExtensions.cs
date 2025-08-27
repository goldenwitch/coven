using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Coven.Core.Di;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder BuildCoven(this IHostApplicationBuilder builder, Action<CovenServiceBuilder> build)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (build is null) throw new ArgumentNullException(nameof(build));
        builder.Services.BuildCoven(build);
        return builder;
    }

    public static CovenServiceBuilder BuildCoven(this IHostApplicationBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        return new CovenServiceBuilder(builder.Services);
    }
}

