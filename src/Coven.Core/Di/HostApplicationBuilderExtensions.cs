// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Hosting;

namespace Coven.Core.Di;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder BuildCoven(this IHostApplicationBuilder builder, Action<CovenServiceBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(build);
        builder.Services.BuildCoven(build);
        return builder;
    }

    public static CovenServiceBuilder BuildCoven(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new CovenServiceBuilder(builder.Services);
    }
}
