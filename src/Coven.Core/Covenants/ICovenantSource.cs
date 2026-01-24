// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Marks an entry type as entering the covenant from outside.
/// Source entries are produced externally (user input, external systems)
/// and do not require an internal producer.
/// </summary>
/// <typeparam name="TCovenant">The covenant this source belongs to.</typeparam>
/// <remarks>
/// An entry implementing <see cref="ICovenantSource{TCovenant}"/> must also implement
/// <see cref="ICovenantEntry{TCovenant}"/>. The analyzer will verify that source entries
/// have at least one consumer within the covenant.
/// </remarks>
public interface ICovenantSource<TCovenant> where TCovenant : ICovenant;
