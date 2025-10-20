// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

public abstract record OpenAIEntry(
    string Sender,
    string Text
);

public sealed record OpenAIOutgoing(
    string Sender,
    string Text
) : OpenAIEntry(Sender, Text);

public sealed record OpenAIIncoming(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender, Text);

public sealed record OpenAIThought(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender, Text);

public sealed record OpenAIAck(
    string Sender,
    string Text
) : OpenAIEntry(Sender, Text);

