namespace Coven.Chat.Adapter.Console;

public sealed record ConsoleAdapterOptions
{
    public string InputSender { get; set; } = "console";
    public bool EchoUserInput { get; set; } = false;
}
