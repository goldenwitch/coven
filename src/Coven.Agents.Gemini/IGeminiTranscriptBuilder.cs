// SPDX-License-Identifier: BUSL-1.1

using Coven.Gemini.Client;

namespace Coven.Agents.Gemini;

internal interface IGeminiTranscriptBuilder
{
    Task<List<GeminiContent>> BuildAsync(GeminiEfferent newest, int maxMessages, CancellationToken cancellationToken);
}
