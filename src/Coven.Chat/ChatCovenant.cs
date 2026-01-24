// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Chat;

/// <summary>
/// Covenant for chat message flows.
/// Guarantees connectivity from user input through windowing to final output.
/// </summary>
public sealed class ChatCovenant : ICovenant
{
    /// <inheritdoc />
    public static string Name => "Chat";
}
