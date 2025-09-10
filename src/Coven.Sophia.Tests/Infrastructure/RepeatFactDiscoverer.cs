using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Coven.Sophia.Tests.Infrastructure;

public sealed class RepeatFactDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink diagnosticMessageSink;

    public RepeatFactDiscoverer(IMessageSink diagnosticMessageSink)
    {
        this.diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        var count = factAttribute.GetNamedArgument<int>(nameof(RepeatFactAttribute.Count));
        if (count <= 0) count = 1;

        for (int i = 0; i < count; i++)
        {
            yield return new XunitTestCase(
                diagnosticMessageSink,
                TestMethodDisplay.ClassAndMethod,
                TestMethodDisplayOptions.None,
                testMethod);
        }
    }
}

