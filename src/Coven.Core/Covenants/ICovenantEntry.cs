// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Marks an entry type as belonging to a covenant.
/// All entries within a covenant must implement this interface,
/// enabling static analysis of the entry flow graph.
/// </summary>
/// <typeparam name="TCovenant">The covenant this entry belongs to.</typeparam>
/// <remarks>
/// Entries that implement only <see cref="ICovenantEntry{TCovenant}"/> (without
/// <see cref="ICovenantSource{TCovenant}"/> or <see cref="ICovenantSink{TCovenant}"/>)
/// are internal to the covenant and must be both produced and consumed within it.
/// </remarks>
public interface ICovenantEntry<TCovenant> where TCovenant : ICovenant;
