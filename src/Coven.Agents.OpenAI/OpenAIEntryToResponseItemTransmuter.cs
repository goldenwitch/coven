// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;
using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Maps OpenAI journal entries to OpenAI SDK <see cref="ResponseItem"/> inputs.
/// Only user/assistant textual entries are emitted; others return null.
/// </summary>
public sealed class OpenAIEntryToResponseItemTransmuter : ITransmuter<OpenAIEntry, ResponseItem?>
{
    public Task<ResponseItem?> Transmute(OpenAIEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIEfferent u => Task.FromResult<ResponseItem?>(ResponseItem.CreateUserMessageItem(u.Text)),
            OpenAIAfferent a => Task.FromResult<ResponseItem?>(ResponseItem.CreateAssistantMessageItem(a.Text)),
            // Thoughts/acks do not participate in prompt construction.
            _ => Task.FromResult<ResponseItem?>(null)
        };
    }
}

