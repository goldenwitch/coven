namespace Coven.Core;

// Descriptor for registered MagikBlocks, including optional capability tags set at registration time.
internal record MagikBlockDescriptor(
    Type InputType,
    Type OutputType,
    object BlockInstance,
    IReadOnlyCollection<string>? Capabilities = null,
    string? DisplayBlockTypeName = null,
    IBlockActivator? Activator = null
);
