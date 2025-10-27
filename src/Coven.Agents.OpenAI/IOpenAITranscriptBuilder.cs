// SPDX-License-Identifier: BUSL-1.1

using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

internal interface IOpenAITranscriptBuilder
{
    Task<List<ResponseItem>> BuildAsync(OpenAIEfferent newest, int maxMessages, CancellationToken cancellationToken);
}

