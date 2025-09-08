
namespace Coven.Spellcasting.Agents.Codex;

internal abstract record TailEvent(string Line, DateTimeOffset Timestamp);
internal sealed record Line(string Line, DateTimeOffset Timestamp) : TailEvent(Line, Timestamp);
internal sealed record ErrorLine(string Line, DateTimeOffset Timestamp) : TailEvent(Line, Timestamp);
