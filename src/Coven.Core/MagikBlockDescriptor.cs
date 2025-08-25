namespace Coven.Core;

// Minimal descriptor for registered MagikBlocks. This is intentionally
// lightweight and immutable; the Board will store a read-only list of these.
public record MagikBlockDescriptor(
    Type InputType,
    Type OutputType,
    object BlockInstance
);

