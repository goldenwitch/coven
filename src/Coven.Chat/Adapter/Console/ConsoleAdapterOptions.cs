// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Adapter.Console;

public sealed record ConsoleAdapterOptions
{
    public string InputSender { get; set; } = "console";
    public bool EchoUserInput { get; set; } = false;
}