// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Marks an entry type as exiting the covenant to outside.
/// Sink entries are consumed externally (sent to users, external systems)
/// and do not require an internal consumer.
/// </summary>
/// <typeparam name="TCovenant">The covenant this sink belongs to.</typeparam>
/// <remarks>
/// An entry implementing <see cref="ICovenantSink{TCovenant}"/> must also implement
/// <see cref="ICovenantEntry{TCovenant}"/>. The validator verifies that sink entries
/// have at least one producer within the covenant.
/// </remarks>
public interface ICovenantSink<TCovenant> where TCovenant : ICovenant;
