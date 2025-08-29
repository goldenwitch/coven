namespace Coven.Spellcasting;

public interface IBook<out T>
{
    T Payload { get; }
}

