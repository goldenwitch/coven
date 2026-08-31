// SPDX-License-Identifier: BUSL-1.1

using CommunityToolkit.Mvvm.ComponentModel;

namespace Coven.Ui.Desktop.ViewModels;

/// <summary>
/// One rendered message in the transcript.
/// </summary>
internal sealed partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string sender, string text, bool isUser, bool isStreaming = false)
    {
        Sender = sender;
        Text = text;
        IsUser = isUser;
        IsStreaming = isStreaming;
    }

    /// <summary>Message body. Grows as streaming fragments arrive.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Whether the message is still being streamed.</summary>
    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    /// <summary>Display label for the author.</summary>
    public string Sender { get; }

    /// <summary>Whether this message came from the user rather than the agent.</summary>
    public bool IsUser { get; }

    /// <summary>
    /// Whether this is the application talking about itself — a failure, a restart — rather
    /// than a turn in the conversation. Those are styled apart so a warning is never mistaken
    /// for something the agent said.
    /// </summary>
    public bool IsSystem => Sender == "system";

    /// <summary>Appends a streaming fragment.</summary>
    public void Append(string fragment) => Text += fragment;
}
