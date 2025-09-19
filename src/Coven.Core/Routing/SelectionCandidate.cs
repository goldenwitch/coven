// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Routing;

// Lightweight, public view of a forward-compatible candidate used by selection strategies.
public sealed class SelectionCandidate
{
    public int RegistryIndex { get; }
    public Type InputType { get; }
    public Type OutputType { get; }
    public string BlockTypeName { get; }
    public IReadOnlyCollection<string> Capabilities { get; }

    internal SelectionCandidate(int registryIndex, Type inputType, Type outputType, string blockTypeName, IReadOnlyCollection<string> capabilities)
    {
        RegistryIndex = registryIndex;
        InputType = inputType;
        OutputType = outputType;
        BlockTypeName = blockTypeName;
        Capabilities = capabilities;
    }
}
