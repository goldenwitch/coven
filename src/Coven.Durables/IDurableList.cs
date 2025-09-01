using System;

namespace Coven.Durables;

public interface IDurableList<T> : IDurable<List<T>>
{
    public Task Append(T Item);
}
