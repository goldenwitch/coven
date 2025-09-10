namespace Coven.Spellcasting.Agents;

// Non-generic agent control surface available via DI for spells/utilities.
public interface IAgentControl
{
    Task CloseAgent();
}

