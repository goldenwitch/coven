// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

// Minimal snapshot of Board internals for immediate test needs.
// Keep lean; expand only when new concrete callers require more data.
internal sealed record BoardStatus(int CompiledPipelinesCount);

