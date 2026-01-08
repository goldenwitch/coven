// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Gemini;

internal interface IGeminiTranscriptBuilder
{
    Task<List<GeminiContent>> BuildAsync(GeminiEfferent newest, int maxMessages, CancellationToken cancellationToken);
}
