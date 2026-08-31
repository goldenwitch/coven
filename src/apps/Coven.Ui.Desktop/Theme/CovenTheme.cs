// SPDX-License-Identifier: BUSL-1.1

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Coven.Ui.Desktop.Theme;

/// <summary>
/// Installs the generated material brushes into the application's resources.
/// </summary>
/// <remarks>
/// The flat colours live in <c>Palette.axaml</c>, where they can be read and edited as a
/// palette should be. Only the textures are built here, because they are pixels rather than
/// values and there is no way to say "paper" in XAML.
/// </remarks>
internal static class CovenTheme
{
    /// <summary>
    /// Builds every texture brush and adds it to <paramref name="resources"/>.
    /// </summary>
    /// <remarks>
    /// Must run before the first window is shown; a <c>DynamicResource</c> that resolves to
    /// nothing leaves the surface untextured rather than raising, which is a failure that
    /// would only ever be noticed by eye.
    /// </remarks>
    public static void Install(IResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        // Seeds are fixed so the grain is identical on every run. A window whose paper
        // reshuffles each launch reads as a rendering fault, not as texture.
        Add(resources, "PaperBrush", PaperTexture.PressedFibre(Color.FromRgb(0xFA, 0xF6, 0xF0), seed: 20792));
        Add(resources, "PaperRaisedBrush", PaperTexture.PressedFibre(Color.FromRgb(0xFF, 0xFD, 0xF8), seed: 30422));
        Add(resources, "PaperSunkBrush", PaperTexture.PressedFibre(Color.FromRgb(0xF0, 0xEA, 0xDA), seed: 40167));
        Add(resources, "ConstructionBrush", PaperTexture.Construction(Color.FromRgb(0x65, 0x39, 0x19), seed: 50801));
        Add(resources, "IndigoWeaveBrush", PaperTexture.Construction(Color.FromRgb(0x55, 0x5B, 0x7B), seed: 60792));
        Add(resources, "FoilGrainBrush", PaperTexture.FoilGrain(seed: 70826));
    }

    /// <summary>Wraps a tile in a brush that repeats it at its natural size.</summary>
    private static void Add(IResourceDictionary resources, string key, WriteableBitmap tile) =>
        resources[key] = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            // Absolute, so the grain stays a fixed physical size instead of stretching with
            // whatever it happens to be painting.
            DestinationRect = new RelativeRect(
                0,
                0,
                PaperTexture.TileSize,
                PaperTexture.TileSize,
                RelativeUnit.Absolute),
        };
}
