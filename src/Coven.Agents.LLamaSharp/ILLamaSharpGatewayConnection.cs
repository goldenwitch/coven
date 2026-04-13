// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Gateway connection for communicating with a locally loaded LLamaSharp model.
/// </summary>
internal interface ILLamaSharpGatewayConnection : IAsyncDisposable
{
    /// <summary>Loads the model and prepares the inference context.</summary>
    Task ConnectAsync();

    /// <summary>Sends a prompt to the local model and writes response entries to the leaf journal.</summary>
    Task SendAsync(LLamaSharpEfferent outgoing, CancellationToken cancellationToken);
}
