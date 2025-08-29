namespace Coven.Spellcasting;

public sealed record Guidebook<TGuide>(TGuide Payload) : IBook<TGuide>;
