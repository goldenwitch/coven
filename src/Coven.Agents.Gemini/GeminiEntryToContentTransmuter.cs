// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.Gemini;

/// <summary>
/// Maps Gemini journal entries to Gemini content parts for transcript construction.
/// Only user/assistant textual entries are emitted; others return null.
/// </summary>
internal sealed class GeminiEntryToContentTransmuter : ITransmuter<GeminiEntry, GeminiContent?>
{
    public Task<GeminiContent?> Transmute(GeminiEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            GeminiEfferent efferent when !string.IsNullOrEmpty(efferent.Text)
                => Task.FromResult<GeminiContent?>(Create("user", efferent.Text)),
            GeminiAfferent afferent when !string.IsNullOrEmpty(afferent.Text)
                => Task.FromResult<GeminiContent?>(Create("model", afferent.Text)),
            _ => Task.FromResult<GeminiContent?>(null)
        };
    }

    private static GeminiContent Create(string role, string text)
        => new()
        {
            Role = role,
            Parts = [new GeminiPart { Text = text }]
        };
}
