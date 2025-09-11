// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat;

// Serialized envelope for file-backed records
internal sealed class RecordWrapper
{
    public long Pos { get; set; }
    public string Type { get; set; } = string.Empty;
    public object? Payload { get; set; }
}
