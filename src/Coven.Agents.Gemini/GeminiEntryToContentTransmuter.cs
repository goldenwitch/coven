// SPDX-License-Identifier: BUSL-1.1

using Coven.Gemini.Client;
using Coven.Transmutation;

namespace Coven.Agents.Gemini;

/// <summary>
/// Maps Gemini journal entries to Gemini content parts for transcript construction.
/// Only user/assistant textual entries are supported; others throw ArgumentOutOfRangeException.
/// Callers should filter entries before transmuting.
/// </summary>
internal sealed class GeminiEntryToContentTransmuter : ITransmuter<GeminiEntry, GeminiContent>
{
    public Task<GeminiContent> Transmute(GeminiEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            GeminiEfferent efferent when !string.IsNullOrEmpty(efferent.Text)
                => Task.FromResult(Create("user", efferent.Text)),
            GeminiAfferent afferent when !string.IsNullOrEmpty(afferent.Text)
                => Task.FromResult(Create("model", afferent.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input),
                $"Cannot transmute {Input.GetType().Name} to GeminiContent. Filter before transmuting.")
        };
    }

    private static GeminiContent Create(string role, string text)
        => new()
        {
            Role = role,
            Parts = [new GeminiPart { Text = text }]
        };
}
