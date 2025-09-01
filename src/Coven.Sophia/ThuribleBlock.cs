using System.Collections.Concurrent;
using Coven.Core;
using Coven.Durables;

namespace Coven.Sophia;

/// <summary>
/// Represents a MagikBlock with built in audit logging.
/// </summary>
/// <typeparam name="TIn"></typeparam>
/// <typeparam name="TOut"></typeparam>
public class ThuribleBlock<TIn, TOut> : IMagikBlock<TIn, TOut>
{
    private readonly Func<TIn, IDurableList<string>, Task<TOut>> _magik;
    private readonly string _label;
    private readonly IDurableList<string> _storage;

    public ThuribleBlock(string Label, IDurableList<string> Storage, Func<TIn, IDurableList<string>, Task<TOut>> Magik)
    {
        _label = Label;
        _magik = Magik;
        _storage = Storage;
    }

    public async Task<TOut> DoMagik(TIn input)
    {

        // Pre-action durable telemetry
        await _storage.Append($"pre-{_label}-log : {DateTimeOffset.UtcNow}");


        // Run our registered lambda. We inject our storage construct so that implementers can leverage the storage for logs.
        TOut result = await _magik(input, _storage);


        // Post-action durable telemetry
        await _storage.Append($"post-{_label}-log : {DateTimeOffset.UtcNow}");
        return result;
    }
}
