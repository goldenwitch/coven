namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal interface ICodexRolloutTranslator<TOut>
{
    // Pure transformation: returns a translated entry or throws if it cannot translate.
    TOut Translate(CodexRolloutEvent ev);
}
