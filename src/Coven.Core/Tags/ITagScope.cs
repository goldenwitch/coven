namespace Coven.Core.Tags;

public interface ITagScope
{
    ISet<string> Set { get; }
    void Add(string tag);
    bool Contains(string tag);
    IEnumerable<string> Enumerate();
}
