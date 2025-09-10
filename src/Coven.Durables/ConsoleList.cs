using System;
using System.Collections.Generic;

namespace Coven.Durables;

// Debug/diagnostic sink: writes appended strings to Console and keeps an in-memory copy.
public sealed class ConsoleList : IDurableList<string>
{
    private readonly List<string> _items = new();

    public Task Append(string item)
    {
        Console.WriteLine(item ?? string.Empty);
        _items.Add(item ?? string.Empty);
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
}

