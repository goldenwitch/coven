// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using Avalonia.Data.Converters;

namespace Coven.Ui.Desktop.Converters;

/// <summary>
/// Labels the refresh button for what it actually does: local models are scanned from disk,
/// hosted models are fetched from the provider.
/// </summary>
internal sealed class RefreshLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Scan folder" : "Refresh list";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("RefreshLabelConverter is one-way.");
}
