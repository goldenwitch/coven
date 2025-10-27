// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

public abstract record OpenAIEntry(
    string Sender
);

public sealed record OpenAIEfferent(
    string Sender,
    string Text
) : OpenAIEntry(Sender);

public sealed record OpenAIAfferent(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

public sealed record OpenAIAfferentChunk(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

public sealed record OpenAIThought(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

public sealed record OpenAIAck(
    string Sender,
    string Text
) : OpenAIEntry(Sender);

public sealed record OpenAIStreamCompleted(
    string Sender,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);
