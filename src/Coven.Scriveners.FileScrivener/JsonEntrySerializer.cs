// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// JSON serializer producing an envelope of <c>{ Position, Entry }</c> using System.Text.Json (web defaults).
/// Intended for newline-delimited output when paired with a file sink.
/// </summary>
public sealed class JsonEntrySerializer<TEntry> : IEntrySerializer<TEntry>
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private sealed record Envelope(long Position, TEntry Entry);

    /// <inheritdoc />
    public string Serialize(long position, TEntry entry)
        => JsonSerializer.Serialize(new Envelope(position, entry), _options);
}
