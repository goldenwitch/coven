// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;
using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIResponseOptionsTransmuter : ITransmuter<OpenAIClientConfig, ResponseCreationOptions>
{
    public Task<ResponseCreationOptions> Transmute(OpenAIClientConfig Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);
        cancellationToken.ThrowIfCancellationRequested();

        ResponseCreationOptions options = new()
        {
            Temperature = Input.Temperature,
            TopP = Input.TopP,
            MaxOutputTokenCount = Input.MaxOutputTokens
        };

        if (Input.Reasoning is not null)
        {
            options.ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = Input.Reasoning.Effort switch
                {
                    ReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
                    ReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
                    ReasoningEffort.High => ResponseReasoningEffortLevel.High,
                    _ => null
                },
                ReasoningSummaryVerbosity = Input.Reasoning.SummaryVerbosity switch
                {
                    ReasoningSummaryVerbosity.Auto => ResponseReasoningSummaryVerbosity.Auto,
                    ReasoningSummaryVerbosity.Detailed => ResponseReasoningSummaryVerbosity.Detailed,
                    ReasoningSummaryVerbosity.Concise => ResponseReasoningSummaryVerbosity.Concise,
                    _ => null
                }
            };
        }

        return Task.FromResult(options);
    }
}
