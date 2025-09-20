// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Routing;

namespace Coven.Core.Builder;

// Shared registry used by both MagikBuilder and the DI builder
internal sealed class MagikRegistry
{
    private readonly List<MagikBlockDescriptor> _registry = [];
    private ISelectionStrategy? _selectionStrategy;

    public void SetSelectionStrategy(ISelectionStrategy strategy)
    {
        _selectionStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

    public void Add(MagikBlockDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _registry.Add(descriptor);
    }

    public IReadOnlyList<MagikBlockDescriptor> Snapshot() => _registry.AsReadOnly();

    public Board BuildBoard(bool pull, PullOptions? pullOptions)
    {
        Board.BoardMode mode = pull ? Board.BoardMode.Pull : Board.BoardMode.Push;
        return new Board(mode, Snapshot(), pullOptions: pullOptions, selectionStrategy: _selectionStrategy);
    }
}

