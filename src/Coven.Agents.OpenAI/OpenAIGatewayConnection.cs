// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIGatewayConnection(
    OpenAIClientConfig configuration,
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ILogger<OpenAIGatewayConnection> logger)
{
    private readonly OpenAIClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        OpenAILog.Connected(_logger);
        return Task.CompletedTask;
    }

    public Task SendAsync(OpenAIOutgoing outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Placeholder: actual OpenAI call is implemented later.
        OpenAILog.OutboundSendStart(_logger, outgoing.Text.Length);
        OpenAILog.OutboundSendSucceeded(_logger);
        return Task.CompletedTask;
    }
}
