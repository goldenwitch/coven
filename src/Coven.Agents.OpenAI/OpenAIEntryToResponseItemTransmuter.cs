// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;
using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Maps OpenAI journal entries to OpenAI SDK <see cref="ResponseItem"/> inputs.
/// Only user (<see cref="OpenAIEfferent"/>) and assistant (<see cref="OpenAIAfferent"/>) entries are supported.
/// Callers must filter entries before transmuting; unsupported types throw <see cref="ArgumentOutOfRangeException"/>.
/// </summary>
internal sealed class OpenAIEntryToResponseItemTransmuter : ITransmuter<OpenAIEntry, ResponseItem>
{
    public Task<ResponseItem> Transmute(OpenAIEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIEfferent u => Task.FromResult<ResponseItem>(ResponseItem.CreateUserMessageItem(u.Text)),
            OpenAIAfferent a => Task.FromResult<ResponseItem>(ResponseItem.CreateAssistantMessageItem(a.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input),
                $"Cannot transmute {Input.GetType().Name} to ResponseItem. Filter before transmuting.")
        };
    }
}

