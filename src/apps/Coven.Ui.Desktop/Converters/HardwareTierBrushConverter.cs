// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Coven.Agents.LLamaSharp;

namespace Coven.Ui.Desktop.Converters;

/// <summary>
/// Colours a hardware tier badge, running green through red as demands rise.
/// </summary>
/// <remarks>
/// Muted, semi-transparent fills so the badge reads on either theme without the label needing
/// its own foreground colour per tier.
/// </remarks>
internal sealed class HardwareTierBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush _low = new(Color.FromArgb(0x55, 0x3F, 0xB9, 0x50));
    private static readonly SolidColorBrush _medium = new(Color.FromArgb(0x55, 0x4A, 0x9E, 0xE0));
    private static readonly SolidColorBrush _high = new(Color.FromArgb(0x55, 0xE0, 0xA0, 0x30));
    private static readonly SolidColorBrush _workstation = new(Color.FromArgb(0x55, 0xE0, 0x5A, 0x4A));
    private static readonly SolidColorBrush _unknown = new(Color.FromArgb(0x33, 0x80, 0x80, 0x80));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is HardwareTier tier
            ? tier switch
            {
                HardwareTier.Low => _low,
                HardwareTier.Medium => _medium,
                HardwareTier.High => _high,
                HardwareTier.Workstation => _workstation,
                _ => _unknown
            }
            : _unknown;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("HardwareTierBrushConverter is one-way.");
}
