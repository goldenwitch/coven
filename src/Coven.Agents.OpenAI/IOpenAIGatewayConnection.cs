// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

internal interface IOpenAIGatewayConnection
{
    Task ConnectAsync();
    Task SendAsync(OpenAIEfferent outgoing, CancellationToken cancellationToken);
}

