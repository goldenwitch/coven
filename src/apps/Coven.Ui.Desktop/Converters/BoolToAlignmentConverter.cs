// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Coven.Ui.Desktop.Converters;

/// <summary>
/// Aligns user messages to the right and everything else to the left.
/// </summary>
internal sealed class BoolToAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("BoolToAlignmentConverter is one-way.");
}
