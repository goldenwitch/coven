using System;
using Xunit;

namespace Coven.Sophia.Tests.Infrastructure;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[Xunit.Sdk.XunitTestCaseDiscoverer(
    "Coven.Sophia.Tests.Infrastructure.RepeatFactDiscoverer",
    "Coven.Sophia.Tests")]
public sealed class RepeatFactAttribute : FactAttribute
{
    public int Count { get; }
    public RepeatFactAttribute(int count) => Count = count;
}

