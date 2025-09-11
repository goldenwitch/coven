// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

// A request to advance one unit of work in Pull mode.
public sealed record GetWorkRequest<TIn>
(
    TIn Input,
    IReadOnlyCollection<string>? Tags = null,
    string? BranchId = null
);