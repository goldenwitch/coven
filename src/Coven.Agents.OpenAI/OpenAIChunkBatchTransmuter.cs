// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Transmutation;

namespace Coven.Agents.OpenAI;

public sealed class OpenAIChunkBatchTransmuter : IBatchTransmuter<OpenAIAfferentChunk, OpenAIThought>
{
    public Task<BatchTransmuteResult<OpenAIAfferentChunk, OpenAIThought>> Transmute(IEnumerable<OpenAIAfferentChunk> Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);

        string sender = string.Empty;
        string responseId = string.Empty;
        string model = string.Empty;
        DateTimeOffset timestamp = DateTimeOffset.MinValue;
        StringBuilder sb = new();

        foreach (OpenAIAfferentChunk chunk in Input)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(chunk.Sender))
            {
                sender = chunk.Sender;
            }
            if (!string.IsNullOrEmpty(chunk.ResponseId))
            {
                responseId = chunk.ResponseId;
            }
            if (!string.IsNullOrEmpty(chunk.Model))
            {
                model = chunk.Model;
            }
            if (chunk.Timestamp != default)
            {
                timestamp = chunk.Timestamp;
            }
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                sb.Append(chunk.Text);
            }
        }

        OpenAIThought output = new(sender, sb.ToString(), responseId, timestamp, model);
        return Task.FromResult(new BatchTransmuteResult<OpenAIAfferentChunk, OpenAIThought>(output, false, null));
    }
}
