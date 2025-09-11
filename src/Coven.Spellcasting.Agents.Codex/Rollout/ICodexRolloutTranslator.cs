// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.Rollout;

public interface ICodexRolloutTranslator<TOut>
{
    // Pure transformation: returns a translated entry or throws if it cannot translate.
    TOut Translate(CodexRolloutLine line);
}