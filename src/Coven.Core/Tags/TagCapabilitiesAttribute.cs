// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class TagCapabilitiesAttribute(params string[] tags) : Attribute
{
    public IReadOnlyCollection<string> Tags { get; } = tags ?? [];
}
