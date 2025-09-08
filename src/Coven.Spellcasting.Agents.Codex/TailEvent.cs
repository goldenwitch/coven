
namespace Coven.Spellcasting.Agents.Codex;

public abstract record TailEvent(string Line, DateTimeOffset Timestamp);
public sealed record Line(string Line, DateTimeOffset Timestamp) : TailEvent(Line, Timestamp);
public sealed record ErrorLine(string Line, DateTimeOffset Timestamp) : TailEvent(Line, Timestamp);
