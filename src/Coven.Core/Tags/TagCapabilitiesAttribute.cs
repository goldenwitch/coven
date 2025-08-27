using System;

namespace Coven.Core.Tags;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class TagCapabilitiesAttribute : Attribute
{
    public IReadOnlyCollection<string> Tags { get; }
    public TagCapabilitiesAttribute(params string[] tags)
    {
        Tags = tags ?? Array.Empty<string>();
    }
}

