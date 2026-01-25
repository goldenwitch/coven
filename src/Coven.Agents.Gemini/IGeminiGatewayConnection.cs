// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Gemini;

internal interface IGeminiGatewayConnection
{
    Task ConnectAsync();
    Task SendAsync(GeminiEfferent outgoing, CancellationToken cancellationToken);
}
