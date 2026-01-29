// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Claude;

internal interface IClaudeGatewayConnection
{
    Task ConnectAsync();
    Task SendAsync(ClaudeEfferent outgoing, CancellationToken cancellationToken);
}
