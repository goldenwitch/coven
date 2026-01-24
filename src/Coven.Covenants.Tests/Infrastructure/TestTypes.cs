// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Covenants.Tests.Infrastructure;

/// <summary>
/// Test covenant for validator tests.
/// </summary>
public sealed class TestCovenant : ICovenant
{
    public static string Name => "Test";
}

/// <summary>
/// Test source entry.
/// </summary>
public sealed record SourceEntry : ICovenantEntry<TestCovenant>, ICovenantSource<TestCovenant>;

/// <summary>
/// Test intermediate entry.
/// </summary>
public sealed record IntermediateEntry : ICovenantEntry<TestCovenant>;

/// <summary>
/// Second intermediate entry for branching scenarios.
/// </summary>
public sealed record IntermediateEntry2 : ICovenantEntry<TestCovenant>;

/// <summary>
/// Third intermediate entry for multi-hop scenarios.
/// </summary>
public sealed record IntermediateEntry3 : ICovenantEntry<TestCovenant>;

/// <summary>
/// Test sink entry.
/// </summary>
public sealed record SinkEntry : ICovenantEntry<TestCovenant>, ICovenantSink<TestCovenant>;

/// <summary>
/// Second sink entry for multiple-sink scenarios.
/// </summary>
public sealed record SinkEntry2 : ICovenantEntry<TestCovenant>, ICovenantSink<TestCovenant>;

/// <summary>
/// Orphaned entry that is consumed but never produced.
/// </summary>
public sealed record OrphanEntry : ICovenantEntry<TestCovenant>;

/// <summary>
/// Dead letter entry that is produced but never consumed.
/// </summary>
public sealed record DeadLetterEntry : ICovenantEntry<TestCovenant>;
