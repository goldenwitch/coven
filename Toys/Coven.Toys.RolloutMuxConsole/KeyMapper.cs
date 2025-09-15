// SPDX-License-Identifier: BUSL-1.1

using System.Text;

namespace Coven.Toys.RolloutMuxConsole;

internal static class KeyMapper
{
    private const char Esc = '\u001b';

    public static bool TryMap(ConsoleKeyInfo key, out string? sequence)
    {
        sequence = null;

        // Printable characters (including Shift-modified symbols)
        if (key.KeyChar != '\0' && !IsSpecialNonPrintable(key))
        {
            if (HasAlt(key))
            {
                sequence = new string(new[] { Esc, key.KeyChar });
                return true;
            }
            sequence = key.KeyChar.ToString();
            return true;
        }

        // Control letters (Ctrl+A .. Ctrl+Z)
        if (HasCtrl(key) && key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.Z)
        {
            char c = (char)('A' + (key.Key - ConsoleKey.A));
            char ctrl = (char)(c & 0x1F);
            sequence = ctrl.ToString();
            return true;
        }

        // Navigation and function keys
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                sequence = Environment.NewLine;
                return true;
            case ConsoleKey.Tab:
                sequence = "\t";
                return true;
            case ConsoleKey.Backspace:
                sequence = "\b";
                return true;
            case ConsoleKey.Escape:
                sequence = Esc.ToString();
                return true;

            case ConsoleKey.UpArrow:
                sequence = CsiCursor('A', key);
                return true;
            case ConsoleKey.DownArrow:
                sequence = CsiCursor('B', key);
                return true;
            case ConsoleKey.RightArrow:
                sequence = CsiCursor('C', key);
                return true;
            case ConsoleKey.LeftArrow:
                sequence = CsiCursor('D', key);
                return true;
            case ConsoleKey.Home:
                sequence = CsiCursor('H', key);
                return true;
            case ConsoleKey.End:
                sequence = CsiCursor('F', key);
                return true;
            case ConsoleKey.PageUp:
                sequence = CsiTilde(5, key);
                return true;
            case ConsoleKey.PageDown:
                sequence = CsiTilde(6, key);
                return true;
            case ConsoleKey.Insert:
                sequence = CsiTilde(2, key);
                return true;
            case ConsoleKey.Delete:
                sequence = CsiTilde(3, key);
                return true;

            // Minimal F-key mapping (no modifiers)
            case ConsoleKey.F1: sequence = "\u001bOP"; return true;
            case ConsoleKey.F2: sequence = "\u001bOQ"; return true;
            case ConsoleKey.F3: sequence = "\u001bOR"; return true;
            case ConsoleKey.F4: sequence = "\u001bOS"; return true;
            case ConsoleKey.F5: sequence = "\u001b[15~"; return true;
            case ConsoleKey.F6: sequence = "\u001b[17~"; return true;
            case ConsoleKey.F7: sequence = "\u001b[18~"; return true;
            case ConsoleKey.F8: sequence = "\u001b[19~"; return true;
            case ConsoleKey.F9: sequence = "\u001b[20~"; return true;
            case ConsoleKey.F10: sequence = "\u001b[21~"; return true;
            case ConsoleKey.F11: sequence = "\u001b[23~"; return true;
            case ConsoleKey.F12: sequence = "\u001b[24~"; return true;
        }

        return false;
    }

    private static bool IsSpecialNonPrintable(ConsoleKeyInfo key)
    {
        // When Ctrl is pressed, KeyChar can be non-zero for some keys; treat these via special paths
        return HasCtrl(key) && char.IsLetter(key.KeyChar);
    }

    private static bool HasShift(ConsoleKeyInfo k) => (k.Modifiers & ConsoleModifiers.Shift) != 0;
    private static bool HasCtrl(ConsoleKeyInfo k) => (k.Modifiers & ConsoleModifiers.Control) != 0;
    private static bool HasAlt(ConsoleKeyInfo k) => (k.Modifiers & ConsoleModifiers.Alt) != 0;

    private static string CsiCursor(char code, ConsoleKeyInfo key)
    {
        int m = ModParam(key);
        if (m == 1) return $"\u001b[{code}";
        return $"\u001b[1;{m}{code}";
    }

    private static string CsiTilde(int number, ConsoleKeyInfo key)
    {
        int m = ModParam(key);
        if (m == 1) return $"\u001b[{number}~";
        return $"\u001b[{number};{m}~";
    }

    private static int ModParam(ConsoleKeyInfo key)
    {
        int m = 1;
        if (HasShift(key)) m += 1;
        if (HasAlt(key)) m += 2;
        if (HasCtrl(key)) m += 4;
        return m;
    }
}
