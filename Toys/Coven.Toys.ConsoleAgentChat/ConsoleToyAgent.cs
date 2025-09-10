using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Spells;
using Coven.Spellcasting.Grimoire;
using Coven.Core;

namespace Coven.Toys.ConsoleAgentChat;

/// <summary>
/// Simple toy agent that uses a guidebook to perform magic-8 ball divination :P
/// </summary>
internal sealed class ConsoleToyAgent : ICovenAgent<ChatEntry>
{
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly string[] _responses;
    private readonly Spellbook _spellbook;
    private readonly Random _rng = new();
    private CancellationTokenSource? _cts;

    public ConsoleToyAgent(IScrivener<ChatEntry> scrivener, Guidebook guidebook, Spellbook spellbook)
    {
        _scrivener = scrivener;
        _spellbook = spellbook;
        // Extract non-empty section bodies from the guidebook to use as canned replies.
        _responses = guidebook.Sections
            .Select(s => (s.Value ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        if (_responses.Length == 0)
        {
            throw new InvalidOperationException("Guidebook sections are empty.");
        }
    }

    public Task RegisterSpells(List<SpellDefinition> spells)
    {
        // No-op: this toy agent does not use spells
        return Task.CompletedTask;
    }

    public async Task InvokeAgent(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CancellationToken token = _cts.Token;

        long after = 0;
        // Tail the chat journal and mirror each user thought with a random response.
        await foreach ((long journalPosition, ChatEntry entry) in _scrivener.TailAsync(after, token).ConfigureAwait(false))
        {
            after = journalPosition;
            if (entry is ChatThought thought)
            {
                var userText = (thought.Text ?? string.Empty).Trim();
                if (string.Equals(userText, "exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(userText, "cancel", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        _ = await _scrivener.WriteAsync(new ChatResponse("agent", "Exiting. Bye!"), token).ConfigureAwait(false);
                    }
                    catch { }
                    try
                    {
                        var cancel = _spellbook.Spells.OfType<CancelAgent>().FirstOrDefault();
                        if (cancel is not null) await cancel.CastSpell().ConfigureAwait(false);
                        else await AmbientAgent.CancelAsync().ConfigureAwait(false);
                    }
                    catch { }
                    break;
                }

                string text = _responses[_rng.Next(_responses.Length)];
                ChatResponse response = new("agent", text);
                try
                {
                    _ = await _scrivener.WriteAsync(response, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public Task CloseAgent()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }
        return Task.CompletedTask;
    }

    Task<ChatEntry> ICovenAgent<ChatEntry>.ReadMessage()
    {
        throw new NotImplementedException();
    }

    Task ICovenAgent<ChatEntry>.SendMessage(ChatEntry message)
    {
        throw new NotImplementedException();
    }
}
