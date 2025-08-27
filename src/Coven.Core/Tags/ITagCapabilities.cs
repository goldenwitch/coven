namespace Coven.Core.Tags;

// Optional interface: blocks can advertise the tags they are capable of handling.
public interface ITagCapabilities
{
    IReadOnlyCollection<string> SupportedTags { get; }
}
