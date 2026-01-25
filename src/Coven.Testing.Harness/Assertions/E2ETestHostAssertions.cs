// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Testing.Harness.Assertions;

/// <summary>
/// Static assertion helper methods for E2E tests.
/// </summary>
public static class E2ETestHostAssertions
{
    /// <summary>
    /// Waits for output that matches the specified predicate.
    /// </summary>
    /// <param name="console">The virtual console to wait on.</param>
    /// <param name="predicate">Predicate to match output against.</param>
    /// <param name="timeout">Maximum time to wait (defaults to 30 seconds).</param>
    /// <param name="because">Optional reason for the assertion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first output line matching the predicate.</returns>
    /// <exception cref="TimeoutException">No matching output within timeout.</exception>
    public static async Task<string> WaitForOutputAsync(
        this VirtualConsoleIO console,
        Func<string, bool> predicate,
        TimeSpan? timeout = null,
        string? because = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        List<string> collected = [];

        try
        {
            while (true)
            {
                string line = await console.WaitForOutputAsync(effectiveTimeout, cts.Token).ConfigureAwait(false);
                collected.Add(line);
                if (predicate(line))
                {
                    return line;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            string message = $"No matching output within {effectiveTimeout}.";
            if (because is not null)
            {
                message += $" Because: {because}.";
            }
            if (collected.Count > 0)
            {
                message += $" Collected {collected.Count} lines: [{string.Join(", ", collected.Select(l => $"\"{l}\""))}]";
            }
            throw new TimeoutException(message);
        }
    }

    /// <summary>
    /// Waits for output that contains the specified text.
    /// </summary>
    /// <param name="console">The virtual console to wait on.</param>
    /// <param name="text">Text to search for in output.</param>
    /// <param name="timeout">Maximum time to wait (defaults to 30 seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first output line containing the text.</returns>
    public static Task<string> WaitForOutputContainingAsync(
        this VirtualConsoleIO console,
        string text,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return console.WaitForOutputAsync(
            line => line.Contains(text, StringComparison.OrdinalIgnoreCase),
            timeout,
            $"expected output containing \"{text}\"",
            cancellationToken);
    }

    /// <summary>
    /// Asserts that no additional output arrives within the timeout period.
    /// Use this to verify the system has quiesced after expected output.
    /// </summary>
    /// <param name="console">The virtual console to check.</param>
    /// <param name="timeout">Time to wait for unexpected output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AssertionException">Unexpected output was received.</exception>
    public static async Task AssertQuietAsync(
        this VirtualConsoleIO console,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string unexpected = await console.WaitForOutputAsync(timeout, cancellationToken).ConfigureAwait(false);
            throw new AssertionException(
                $"Expected no output within {timeout}, but received: \"{unexpected}\"");
        }
        catch (TimeoutException)
        {
            // Expected - no output within timeout means success
        }
    }

    /// <summary>
    /// Asserts that the console output contains the expected lines in order.
    /// </summary>
    /// <param name="console">The virtual console to check.</param>
    /// <param name="expectedLines">The expected output lines in order.</param>
    /// <param name="timeout">Maximum time to wait for all lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AssertionException">Output did not match expected lines.</exception>
    public static async Task AssertOutputInOrderAsync(
        this VirtualConsoleIO console,
        IEnumerable<string> expectedLines,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        List<string> expected = [.. expectedLines];
        IReadOnlyList<string> actual = await console.CollectOutputAsync(expected.Count, effectiveTimeout, cancellationToken).ConfigureAwait(false);

        for (int i = 0; i < expected.Count; i++)
        {
            if (actual[i] != expected[i])
            {
                throw new AssertionException(
                    $"Output mismatch at line {i}. Expected: \"{expected[i]}\", Actual: \"{actual[i]}\"");
            }
        }
    }

    /// <summary>
    /// Asserts that all sent messages match the expected predicates in order.
    /// </summary>
    /// <param name="gateway">The virtual Discord gateway.</param>
    /// <param name="predicates">Predicates to match against sent messages.</param>
    /// <exception cref="AssertionException">Sent messages did not match expected predicates.</exception>
    public static void AssertSentMessagesInOrder(
        this VirtualDiscordGateway gateway,
        params Func<OutboundMessage, bool>[] predicates)
    {
        IReadOnlyList<OutboundMessage> messages = gateway.SentMessages;

        if (messages.Count < predicates.Length)
        {
            throw new AssertionException(
                $"Expected at least {predicates.Length} sent messages, but found {messages.Count}.");
        }

        for (int i = 0; i < predicates.Length; i++)
        {
            if (!predicates[i](messages[i]))
            {
                throw new AssertionException(
                    $"Sent message {i} did not match predicate. Message: ChannelId={messages[i].ChannelId}, Content=\"{messages[i].Content}\"");
            }
        }
    }

    /// <summary>
    /// Asserts that a message was sent to the specified channel with the expected content.
    /// </summary>
    /// <param name="gateway">The virtual Discord gateway.</param>
    /// <param name="channelId">Expected channel ID.</param>
    /// <param name="contentPredicate">Predicate for message content.</param>
    /// <exception cref="AssertionException">No matching message was found.</exception>
    public static void AssertSentMessage(
        this VirtualDiscordGateway gateway,
        ulong channelId,
        Func<string, bool> contentPredicate)
    {
        IReadOnlyList<OutboundMessage> messages = gateway.SentMessages;
        OutboundMessage? match = messages.FirstOrDefault(m => m.ChannelId == channelId && contentPredicate(m.Content));

        if (match is null)
        {
            List<OutboundMessage> channelMessages = [.. messages.Where(m => m.ChannelId == channelId)];
            if (channelMessages.Count == 0)
            {
                throw new AssertionException(
                    $"No messages sent to channel {channelId}. Total messages: {messages.Count}");
            }
            else
            {
                throw new AssertionException(
                    $"No matching message content for channel {channelId}. " +
                    $"Found {channelMessages.Count} messages: [{string.Join(", ", channelMessages.Select(m => $"\"{m.Content}\""))}]");
            }
        }
    }
}

/// <summary>
/// Exception thrown when an E2E test assertion fails.
/// </summary>
public sealed class AssertionException(string message) : Exception(message);
