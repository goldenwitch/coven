// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;

namespace Coven.Core.Tests.Infrastructure;

// Common test blocks to reduce per-test boilerplate.

internal sealed class StringLengthBlock : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
}

internal sealed class StringHashBlock : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.GetHashCode());
}

internal sealed class IntToDoubleBlock : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
}

internal sealed class IntToDoubleAddOneBlock : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult(input + 1d);
}

internal sealed class IntToDoubleAddBlock(double delta) : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult(input + delta);
}

internal sealed class ReturnConstIntBlock(int value) : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(value);
}

internal sealed class AsyncDelayStringLengthBlock(int delayMs) : IMagikBlock<string, int>
{
    public async Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        return input.Length;
    }
}

internal sealed class AsyncDelayIntToDoubleBlock(int delayMs) : IMagikBlock<int, double>
{
    public async Task<double> DoMagik(int input, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        return input;
    }
}

internal sealed class EmitFastBlock : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        Tag.Add("fast");
        return Task.FromResult(input.Length);
    }
}

internal sealed class EmitManyBlock : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        Tag.Add("fast");
        Tag.Add("gpu");
        Tag.Add("ai");
        return Task.FromResult(input.Length);
    }
}

[TagCapabilities("fast")]
internal sealed class CapFastBlock : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i);
}

internal sealed class CapParamlessFastBlock : IMagikBlock<int, double>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => ["fast"];
    public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult(i + 2000d);
}

[TagCapabilities("fast")]
internal sealed class CapMergedBlock : IMagikBlock<int, double>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => ["gpu"];
    public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult(i + 3000d);
}

// Tag routing helpers

// Probe for Tag scope
internal sealed class ProbeTagBlock : IMagikBlock<string, string>
{
    public Task<string> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        Tag.Add("probe");
        bool ok = Tag.Contains("probe");
        return Task.FromResult(ok ? "ok" : "bad");
    }
}

// Counter and math helpers
internal sealed class Counter { public int Value { get; init; } }

internal sealed class IncBlock : IMagikBlock<Counter, Counter>
{
    public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default)
        => Task.FromResult(new Counter { Value = input.Value + 1 });
}


internal sealed class Copy1Block : IMagikBlock<Counter, Counter>
{
    public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(new Counter { Value = input.Value });
}

internal sealed class Copy2Block : IMagikBlock<Counter, Counter>
{
    public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(new Counter { Value = input.Value });
}

internal sealed class CounterToDoubleBlock : IMagikBlock<Counter, double>
{
    public Task<double> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult((double)input.Value);
}

// Pull behavior helpers
internal sealed class Start { public string Value { get; init; } = string.Empty; }

internal sealed class ToObjectBlock : IMagikBlock<Start, object>
{
    public Task<object> DoMagik(Start input, CancellationToken cancellationToken = default) => Task.FromResult((object)input.Value);
}

internal sealed class ObjectToStringPlus : IMagikBlock<object, string>
{
    public Task<string> DoMagik(object input, CancellationToken cancellationToken = default) => Task.FromResult(((string)input) + "|b2");
}

// Halo demo helpers
internal sealed class Doc { public string Text { get; init; } = string.Empty; }

internal sealed class ParseAndTag : IMagikBlock<string, Doc>
{
    public Task<Doc> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        if (input.Contains('!'))
        {
            Tag.Add("exclaim");
        }


        Tag.Add("style:loud");
        return Task.FromResult(new Doc { Text = input });
    }
}

internal sealed class AddSalutation : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => ["exclaim"];
    public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default)
    {
        string text = $"☀ PRAISE THE SUN! ☀ {input.Text} — If only I could be so grossly incandescent.";
        return Task.FromResult(new Doc { Text = text });
    }
}

internal sealed class UppercaseText : IMagikBlock<Doc, Doc>
{
    public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
}

internal sealed class LowercaseText : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => ["style:quiet"];
    public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToLowerInvariant() });
}

internal sealed class DocToOut : IMagikBlock<Doc, string>
{
    public Task<string> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(input.Text);
}
