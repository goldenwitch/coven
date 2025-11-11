namespace Coven.Core;

/// <summary>
/// Marker base type for journal entries.
/// </summary>
public abstract record Entry;

/// <summary>
/// Marker interface for draft entries.
/// </summary>
public interface IDraft { }
