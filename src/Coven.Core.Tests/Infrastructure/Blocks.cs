// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Tags;

namespace Coven.Core.Tests.Infrastructure;

// Common test blocks to reduce per-test boilerplate.

internal sealed class StringLength : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
}

internal sealed class StringHash : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.GetHashCode());
}

internal sealed class IntToDouble : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
}

internal sealed class IntToDoubleAddOne : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult(input + 1d);
}

internal sealed class IntToDoubleAdd : IMagikBlock<int, double>
{
    private readonly double delta;
    public IntToDoubleAdd(double delta) { this.delta = delta; }
    public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input + delta);
}

internal sealed class ReturnConstInt : IMagikBlock<string, int>
{
    private readonly int value;
    public ReturnConstInt(int value) { this.value = value; }
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(value);
}

internal sealed class AsyncDelayStringLength : IMagikBlock<string, int>
{
    private readonly int delayMs;
    public AsyncDelayStringLength(int delayMs) { this.delayMs = delayMs; }
    public async Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        return input.Length;
    }
}

internal sealed class AsyncDelayIntToDouble : IMagikBlock<int, double>
{
    private readonly int delayMs;
    public AsyncDelayIntToDouble(int delayMs) { this.delayMs = delayMs; }
    public async Task<double> DoMagik(int input, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        return (double)input;
    }
}

internal sealed class EmitFast : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        Tag.Add("fast");
        return Task.FromResult(input.Length);
    }
}

internal sealed class EmitMany : IMagikBlock<string, int>
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
internal sealed class CapFast : IMagikBlock<int, double>
{
    public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i);
}

internal sealed class CapParamlessFast : IMagikBlock<int, double>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => new[] { "fast" };
    public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i + 2000d);
}

[TagCapabilities("fast")]
internal sealed class CapMerged : IMagikBlock<int, double>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => new[] { "gpu" };
    public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i + 3000d);
}

// Tag routing helpers

// Probe for Tag scope
internal sealed class ProbeTag : IMagikBlock<string, string>
{
    public Task<string> DoMagik(string input, CancellationToken cancellationToken = default)
    {
        Tag.Add("probe");
        var ok = Tag.Contains("probe");
        return Task.FromResult(ok ? "ok" : "bad");
    }
}

// Counter and math helpers
internal sealed class Counter { public int Value { get; init; } }

internal sealed class Inc : IMagikBlock<Counter, Counter>
{
    public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default)
        => Task.FromResult(new Counter { Value = input.Value + 1 });
}


internal sealed class Copy1 : IMagikBlock<Counter, Counter>
{
    public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(new Counter { Value = input.Value });
}

internal sealed class Copy2 : IMagikBlock<Counter, Counter>
{
    public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(new Counter { Value = input.Value });
}

internal sealed class CounterToDouble : IMagikBlock<Counter, double>
{
    public Task<double> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult((double)input.Value);
}

// Pull behavior helpers
internal sealed class Start { public string Value { get; init; } = string.Empty; }

internal sealed class ToObject : IMagikBlock<Start, object>
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
        if (input.Contains('!')) Tag.Add("exclaim");
        Tag.Add("style:loud");
        return Task.FromResult(new Doc { Text = input });
    }
}

internal sealed class AddSalutation : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => new[] { "exclaim" };
    public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default)
    {
        var text = $"☀ PRAISE THE SUN! ☀ {input.Text} — If only I could be so grossly incandescent.";
        return Task.FromResult(new Doc { Text = text });
    }
}

internal sealed class UppercaseText : IMagikBlock<Doc, Doc>
{
    public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
}

internal sealed class LowercaseText : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => new[] { "style:quiet" };
    public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToLowerInvariant() });
}

internal sealed class DocToOut : IMagikBlock<Doc, string>
{
    public Task<string> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(input.Text);
}
