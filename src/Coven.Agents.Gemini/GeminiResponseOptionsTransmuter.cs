// SPDX-License-Identifier: BUSL-1.1

using Coven.Gemini.Client;
using Coven.Transmutation;

namespace Coven.Agents.Gemini;

internal sealed record GeminiRequestOptions(
    GeminiGenerationConfig? Generation,
    List<GeminiSafetySettingDto>? SafetySettings,
    GeminiContent? SystemInstruction);

internal sealed class GeminiResponseOptionsTransmuter : ITransmuter<GeminiClientConfig, GeminiRequestOptions>
{
    public Task<GeminiRequestOptions> Transmute(GeminiClientConfig Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);
        cancellationToken.ThrowIfCancellationRequested();

        GeminiGenerationConfig? generation = null;
        if (Input.Temperature.HasValue || Input.TopP.HasValue || Input.TopK.HasValue || Input.MaxOutputTokens.HasValue || !string.IsNullOrWhiteSpace(Input.ResponseMimeType))
        {
            generation = new GeminiGenerationConfig
            {
                Temperature = Input.Temperature,
                TopP = Input.TopP,
                TopK = Input.TopK,
                MaxOutputTokens = Input.MaxOutputTokens,
                ResponseMimeType = Input.ResponseMimeType
            };
        }

        List<GeminiSafetySettingDto>? safetySettings = null;
        if (Input.SafetySettings is not null)
        {
            safetySettings = [];
            foreach (GeminiSafetySetting s in Input.SafetySettings)
            {
                if (string.IsNullOrWhiteSpace(s.Category) || string.IsNullOrWhiteSpace(s.Threshold))
                {
                    continue;
                }
                safetySettings.Add(new GeminiSafetySettingDto
                {
                    Category = s.Category,
                    Threshold = s.Threshold
                });
            }
            if (safetySettings.Count == 0)
            {
                safetySettings = null;
            }
        }

        GeminiContent? systemInstruction = null;
        if (!string.IsNullOrWhiteSpace(Input.SystemInstruction))
        {
            systemInstruction = new GeminiContent
            {
                Role = "system",
                Parts = [new GeminiPart { Text = Input.SystemInstruction }]
            };
        }

        return Task.FromResult(new GeminiRequestOptions(generation, safetySettings, systemInstruction));
    }
}
