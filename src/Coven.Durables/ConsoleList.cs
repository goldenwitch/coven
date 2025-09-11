// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coven.Durables;

// Debug/diagnostic sink: writes appended strings to Console and keeps an in-memory copy.
public sealed class ConsoleList : IDurableList<string>
{
    private readonly List<string> _items = new();

    public Task Append(string item)
    {
        var line = item ?? string.Empty;
        WriteColored(line);
        _items.Add(line);
        return Task.CompletedTask;
    }

    public Task<List<string>> Load()
    {
        return Task.FromResult(new List<string>(_items));
    }

    public Task Save(List<string> input)
    {
        _items.Clear();
        if (input is not null) _items.AddRange(input);
        return Task.CompletedTask;
    }

    private static void WriteColored(string entry)
    {
        var originalColor = Console.ForegroundColor;
        try
        {
            // Split into prefix, message, and scopes: "<prefix> :: <message> | Scopes=[...]"
            int sepIdx = entry.IndexOf(" :: ", StringComparison.Ordinal);
            int scopesIdx = entry.IndexOf(" | Scopes=", StringComparison.Ordinal);

            string prefix = sepIdx >= 0 ? entry.Substring(0, sepIdx) : entry;
            string message = sepIdx >= 0
                ? (scopesIdx > sepIdx ? entry.Substring(sepIdx + 4, scopesIdx - (sepIdx + 4)) : entry.Substring(sepIdx + 4))
                : string.Empty;
            string scopes = scopesIdx >= 0 ? entry.Substring(scopesIdx) : string.Empty;

            // Timestamp [..] in dark gray
            if (prefix.StartsWith("[", StringComparison.Ordinal))
            {
                int endBracket = prefix.IndexOf(']');
                if (endBracket > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(prefix.Substring(0, endBracket + 1));
                    if (endBracket + 1 < prefix.Length) Console.Write(' ');
                    prefix = prefix.Substring(Math.Min(endBracket + 2, prefix.Length));
                }
            }

            // Library/category portion in cyan
            if (!string.IsNullOrEmpty(prefix))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(prefix);
                if (sepIdx >= 0) Console.Write(" :: ");
            }

            // Message in white
            if (!string.IsNullOrEmpty(message))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(message);
            }

            // Scopes in magenta
            if (!string.IsNullOrEmpty(scopes))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(scopes);
            }

            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }
}