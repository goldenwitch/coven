namespace Coven.Spellcasting;

public sealed record Testbook<TTest>(TTest Payload) : IBook<TTest>;
