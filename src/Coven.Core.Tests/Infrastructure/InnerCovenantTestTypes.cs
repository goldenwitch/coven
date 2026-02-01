// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tests.Infrastructure;

/// <summary>
/// Test entry types for inner covenant builder testing.
/// </summary>
public abstract record BoundaryEntry : Entry;
public record BoundaryInput : BoundaryEntry;
public record BoundaryOutput : BoundaryEntry;
public record BoundaryFault : BoundaryEntry;

public abstract record InnerAEntry : Entry;
public record InnerAInput : InnerAEntry;
public record InnerAOutput : InnerAEntry;
