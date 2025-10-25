// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Composes multiple windowing policies. Emits when any policy suggests emit.
/// Uses the maximum MinChunkLookback across children to ensure sufficient context.
/// </summary>
public sealed class CompositeWindowPolicy<TChunk> : IWindowPolicy<TChunk>
{
    private readonly IReadOnlyList<IWindowPolicy<TChunk>> _policies;

    public CompositeWindowPolicy(params IWindowPolicy<TChunk>[] policies)
    {
        if (policies is null || policies.Length == 0)
        {
            throw new ArgumentException("At least one window policy is required", nameof(policies));
        }
        _policies = policies;
        MinChunkLookback = _policies.Max(s => s.MinChunkLookback);
    }

    public int MinChunkLookback { get; }

    public bool ShouldEmit(StreamWindow<TChunk> window)
        => _policies.Any(s => s.ShouldEmit(window));
}

