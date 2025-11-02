// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Routing;

/// <summary>
/// Public view of a forward-compatible candidate used by selection strategies.
/// </summary>
public sealed class SelectionCandidate
{
    /// <summary>Index in the registration registry.</summary>
    public int RegistryIndex { get; }
    /// <summary>Input type accepted by the candidate.</summary>
    public Type InputType { get; }
    /// <summary>Output type produced by the candidate.</summary>
    public Type OutputType { get; }
    /// <summary>Display name of the block type.</summary>
    public string BlockTypeName { get; }
    /// <summary>Capability tags advertised by the block.</summary>
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
