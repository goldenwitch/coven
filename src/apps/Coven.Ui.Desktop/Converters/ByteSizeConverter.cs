// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using Avalonia.Data.Converters;
using Coven.Agents.LLamaSharp;

namespace Coven.Ui.Desktop.Converters;

/// <summary>
/// Formats a byte count for display, e.g. <c>4.6 GB</c>.
/// </summary>
internal sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long bytes && bytes > 0 ? LocalModelCatalog.FormatSize(bytes) : "size unknown";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("ByteSizeConverter is one-way.");
}
