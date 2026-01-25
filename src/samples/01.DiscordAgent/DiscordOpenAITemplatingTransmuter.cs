// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.OpenAI;
using Coven.Transmutation;
using OpenAI.Responses;

namespace DiscordAgent;

/// <summary>
/// Example customization: demonstrates how an app can override the default
/// OpenAI entry → ResponseItem mapping to inject lightweight templating.
///
/// Notes
/// - Keeps assistant/user roles but decorates text with Discord-specific
///   markers. Real apps could inject persona, routing hints, etc.
/// - Callers must filter entries before transmuting; unsupported types throw.
/// </summary>
internal sealed class DiscordOpenAITemplatingTransmuter : ITransmuter<OpenAIEntry, ResponseItem>
{
    public Task<ResponseItem> Transmute(OpenAIEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            // Decorate outgoing (user) content with a simple Discord marker.
            OpenAIEfferent u => Task.FromResult<ResponseItem>(
                ResponseItem.CreateUserMessageItem($"[discord username:{u.Sender}] {u.Text}")),

            // Decorate assistant content for clarity in logs and evals.
            OpenAIAfferent a => Task.FromResult<ResponseItem>(
                ResponseItem.CreateAssistantMessageItem($"[assistant:{a.Model}] {a.Text}")),

            _ => throw new ArgumentOutOfRangeException(nameof(Input),
                $"Cannot transmute {Input.GetType().Name} to ResponseItem. Filter before transmuting.")
        };
    }
}

