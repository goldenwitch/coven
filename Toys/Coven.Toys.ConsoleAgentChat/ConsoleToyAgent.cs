using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Spells;

namespace Coven.Toys.ConsoleAgentChat;

/// <summary>
/// Simple toy agent that uses a guidebook to perform magic-8 ball divination :P
/// </summary>
internal sealed class ConsoleToyAgent : ICovenAgent<ChatEntry>
{
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly string[] _responses;
    private readonly Random _rng = new();
    private CancellationTokenSource? _cts;

    public ConsoleToyAgent(IScrivener<ChatEntry> scrivener, Guidebook guidebook)
    {
        _scrivener = scrivener;
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
        var token = _cts.Token;

        long after = 0;
        await foreach (var pair in _scrivener.TailAsync(after, token).ConfigureAwait(false))
        {
            after = pair.journalPosition;
            if (pair.entry is ChatThought)
            {
                var text = _responses[_rng.Next(_responses.Length)];
                var response = new ChatResponse("agent", text);
                try { _ = await _scrivener.WriteAsync(response, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public Task CloseAgent()
    {
        try { _cts?.Cancel(); }
        catch { }
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
