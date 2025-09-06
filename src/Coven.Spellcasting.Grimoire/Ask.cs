using Coven.Chat;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Grimoire;

public class Ask : ISpell<string, string>
{
    private readonly IScrivener<ChatEntry> _scrivener;

    public Ask(IScrivener<ChatEntry> scrivener)
    {
        _scrivener = scrivener;
    }

    public async Task<string> CastSpell(string Input)
    {
        // Write the prompt as a thought from the agent and wait for a response.
        var anchor = await _scrivener.WriteAsync(new ChatThought("Agent", Input));
        var (_, response) = await _scrivener.WaitForAsync<ChatResponse>(anchor);
        return response.Text;
    }
}
