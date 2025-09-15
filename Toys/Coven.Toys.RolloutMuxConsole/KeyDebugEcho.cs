// SPDX-License-Identifier: BUSL-1.1

using System.Text;

namespace Coven.Toys.RolloutMuxConsole;

/// <summary>
/// Debug-only echo for key events, kept separate from Program.cs to isolate responsibility.
/// Writes to stderr so it doesn’t mix with normal stdout content.
/// </summary>
internal static class KeyDebugEcho
{
    // Default to enabled in DEBUG builds; callers need not set this.
#if DEBUG
    public static bool Enabled { get; set; } = true;
#else
    public static bool Enabled { get; set; } = false;
#endif

    public static void Raw(ConsoleKeyInfo key)
    {
#if DEBUG
        if (!Enabled) return;
        try
        {
            var meta = Describe(key);
            Console.Error.WriteLine($"[key-raw] {meta}");
        }
        catch { }
#endif
    }

    public static void Mapped(ConsoleKeyInfo key, string sequence)
    {
#if DEBUG
        if (!Enabled) return;
        try
        {
            var meta = Describe(key);
            Console.Error.WriteLine($"[key-map] {meta} => {ToVisible(sequence)}");
        }
        catch { }
#endif
    }

    public static void Info(string message)
    {
#if DEBUG
        if (!Enabled) return;
        try { Console.Error.WriteLine($"[key-info] {message}"); } catch { }
#endif
    }

    private static string Describe(ConsoleKeyInfo key)
    {
        var parts = new List<string>(4) { key.Key.ToString() };
        if (key.KeyChar != '\0') parts.Add($"char={ToVisible(new string(key.KeyChar, 1))}");
        if ((key.Modifiers & ConsoleModifiers.Control) != 0) parts.Add("Ctrl");
        if ((key.Modifiers & ConsoleModifiers.Alt) != 0) parts.Add("Alt");
        if ((key.Modifiers & ConsoleModifiers.Shift) != 0) parts.Add("Shift");
        return string.Join(" ", parts);
    }

    private static string ToVisible(string s)
    {
        var sb = new StringBuilder(s.Length * 2);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '\\' => "\\\\",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '\b' => "\\b",
                '\u001b' => "<ESC>",
                '\u0003' => "<ETX>",
                char c when char.IsControl(c) => $"<0x{(int)c:X2}>",
                var other => other.ToString()
            });
        }
        return sb.ToString();
    }
}
