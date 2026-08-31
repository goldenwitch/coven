// SPDX-License-Identifier: BUSL-1.1

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Coven.Ui.Desktop.Theme;

/// <summary>
/// Draws the tileable material textures the interface is built from.
/// </summary>
/// <remarks>
/// These are generated rather than shipped as image files for two reasons: a paper grain is a
/// few hundred lines of noise but hundreds of kilobytes of PNG, and generating it means the
/// tint is a parameter rather than a fixed asset — the same fibre structure can be dyed to any
/// thread colour in the palette.
/// <para>
/// Every texture tiles seamlessly, which is what all the wrapping arithmetic is for: a fibre
/// running off the right edge is drawn back onto the left, so a wall of tiles has no grid.
/// </para>
/// </remarks>
internal static class PaperTexture
{
    /// <summary>
    /// Edge length of a tile. Large enough that repetition is not obvious on a full window,
    /// small enough that the whole set costs well under a megabyte.
    /// </summary>
    public const int TileSize = 256;

    /// <summary>Scales a count written for one tile so density holds if the tile is resized.</summary>
    private static int Density(int perTile) => perTile * TileSize * TileSize / (192 * 192);

    /// <summary>
    /// Paper pressed from fine pulp: dense short fibres, a faint cloudiness from uneven
    /// pressing, and a light per-pixel tooth.
    /// </summary>
    public static WriteableBitmap PressedFibre(Color tint, int seed)
    {
        int[] pixels = Flood(tint);
        Random random = new(seed);

        // Deliberately faint. Cloudiness is the one component with structure large enough to
        // be recognised from tile to tile, so anything stronger turns a wall of paper into a
        // visible grid of identical blotches.
        Cloud(pixels, random, lattice: 6, amplitude: 1.4);

        // Long, pale, low-contrast fibres are what separate paper from flat colour. They run
        // in every direction because pressed pulp has no grain — unlike, say, wood.
        for (int i = 0; i < Density(1400); i++)
        {
            Fibre(
                pixels,
                random,
                length: random.Next(8, 30),
                shade: random.NextDouble() < 0.55 ? 26 : -20,
                alpha: 0.018 + (random.NextDouble() * 0.045));
        }

        Tooth(pixels, random, amplitude: 4);
        return ToBitmap(pixels);
    }

    /// <summary>
    /// Construction paper: coarse, cheaply pulped stock, its surface flecked with the
    /// unbleached specks that never broke down.
    /// </summary>
    public static WriteableBitmap Construction(Color tint, int seed)
    {
        int[] pixels = Flood(tint);
        Random random = new(seed);

        // Construction paper only ever covers small elements, where a tile never repeats
        // enough to give itself away, so it can keep the mottling that makes it look cheap.
        Cloud(pixels, random, lattice: 5, amplitude: 6);

        for (int i = 0; i < Density(700); i++)
        {
            Fibre(
                pixels,
                random,
                length: random.Next(5, 18),
                shade: random.NextDouble() < 0.5 ? 40 : -28,
                alpha: 0.03 + (random.NextDouble() * 0.07));
        }

        // The flecks. Bright, short and sparse — a few pixels of pulp that escaped the dye,
        // which is the single most recognisable thing about construction paper.
        for (int i = 0; i < Density(320); i++)
        {
            int x = random.Next(TileSize);
            int y = random.Next(TileSize);
            int radius = random.NextDouble() < 0.8 ? 0 : 1;
            Color fleck = Shift(tint, random.NextDouble() < 0.75 ? 62 : -34);
            double alpha = 0.18 + (random.NextDouble() * 0.35);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    Blend(pixels, x + dx, y + dy, fleck, alpha);
                }
            }
        }

        Tooth(pixels, random, amplitude: 7);
        return ToBitmap(pixels);
    }

    /// <summary>
    /// The grain of stamped foil, as transparent light and shadow to be laid over a metallic
    /// gradient. Foil is rolled, so unlike paper its structure is directional: fine parallel
    /// striations that catch the light along one axis.
    /// </summary>
    public static WriteableBitmap FoilGrain(int seed)
    {
        int[] pixels = new int[TileSize * TileSize];
        Random random = new(seed);

        for (int i = 0; i < Density(2600); i++)
        {
            int x = random.Next(TileSize);
            int y = random.Next(TileSize);
            int length = random.Next(6, 26);
            bool bright = random.NextDouble() < 0.5;
            Color shade = bright ? Colors.White : Colors.Black;
            double alpha = (bright ? 0.10 : 0.07) * random.NextDouble();

            // Near-horizontal, with a little scatter so it reads as brushed rather than ruled.
            double angle = (random.NextDouble() - 0.5) * 0.34;
            double dx = Math.Cos(angle);
            double dy = Math.Sin(angle);

            for (int step = 0; step < length; step++)
            {
                double fade = 1 - (Math.Abs((step / (double)length) - 0.5) * 2);
                Blend(pixels, (int)(x + (dx * step)), (int)(y + (dy * step)), shade, alpha * fade);
            }
        }

        return ToBitmap(pixels);
    }

    /// <summary>Fills a tile with a flat opaque colour.</summary>
    private static int[] Flood(Color tint)
    {
        int[] pixels = new int[TileSize * TileSize];
        int packed = Pack(tint.R, tint.G, tint.B, 255);
        Array.Fill(pixels, packed);
        return pixels;
    }

    /// <summary>
    /// Adds broad, soft variation — the unevenness of a pressed sheet, which keeps a large
    /// area from looking like a flat fill dusted with noise.
    /// </summary>
    private static void Cloud(int[] pixels, Random random, int lattice, double amplitude)
    {
        double[,] grid = new double[lattice, lattice];
        for (int y = 0; y < lattice; y++)
        {
            for (int x = 0; x < lattice; x++)
            {
                grid[y, x] = (random.NextDouble() * 2) - 1;
            }
        }

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                double value = Sample(grid, lattice, x / (double)TileSize * lattice, y / (double)TileSize * lattice);
                Shade(pixels, x, y, value * amplitude);
            }
        }
    }

    /// <summary>Bilinear sample of the lattice, wrapping so the result tiles.</summary>
    private static double Sample(double[,] grid, int lattice, double x, double y)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        double fx = x - x0;
        double fy = y - y0;

        // Smoothstep, so the lattice reads as cloud rather than as a quilt of triangles.
        fx = fx * fx * (3 - (2 * fx));
        fy = fy * fy * (3 - (2 * fy));

        double a = grid[Wrap(y0, lattice), Wrap(x0, lattice)];
        double b = grid[Wrap(y0, lattice), Wrap(x0 + 1, lattice)];
        double c = grid[Wrap(y0 + 1, lattice), Wrap(x0, lattice)];
        double d = grid[Wrap(y0 + 1, lattice), Wrap(x0 + 1, lattice)];

        double top = a + ((b - a) * fx);
        double bottom = c + ((d - c) * fx);
        return top + ((bottom - top) * fy);
    }

    /// <summary>Draws one fibre, wrapping at the edges so the tile stays seamless.</summary>
    private static void Fibre(int[] pixels, Random random, int length, int shade, double alpha)
    {
        double angle = random.NextDouble() * Math.PI * 2;
        double dx = Math.Cos(angle);
        double dy = Math.Sin(angle);
        int x = random.Next(TileSize);
        int y = random.Next(TileSize);

        for (int step = 0; step < length; step++)
        {
            // Fade both ends, so fibres melt into the sheet instead of stopping dead.
            double fade = 1 - (Math.Abs((step / (double)length) - 0.5) * 2);
            Shade(pixels, (int)(x + (dx * step)), (int)(y + (dy * step)), shade * alpha * fade * 4);
        }
    }

    /// <summary>Per-pixel roughness — the tooth you feel on uncoated stock.</summary>
    private static void Tooth(int[] pixels, Random random, int amplitude)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            double delta = ((random.NextDouble() * 2) - 1) * amplitude;
            int dst = pixels[i];
            pixels[i] = Pack(
                Clamp(((dst >> 16) & 0xFF) + delta),
                Clamp(((dst >> 8) & 0xFF) + delta),
                Clamp((dst & 0xFF) + delta),
                (dst >> 24) & 0xFF);
        }
    }

    /// <summary>Lightens or darkens one pixel, wrapping its coordinates.</summary>
    private static void Shade(int[] pixels, int x, int y, double delta)
    {
        int i = (Wrap(y, TileSize) * TileSize) + Wrap(x, TileSize);
        int dst = pixels[i];
        pixels[i] = Pack(
            Clamp(((dst >> 16) & 0xFF) + delta),
            Clamp(((dst >> 8) & 0xFF) + delta),
            Clamp((dst & 0xFF) + delta),
            (dst >> 24) & 0xFF);
    }

    /// <summary>Composites a colour over one pixel, wrapping its coordinates.</summary>
    private static void Blend(int[] pixels, int x, int y, Color color, double alpha)
    {
        int i = (Wrap(y, TileSize) * TileSize) + Wrap(x, TileSize);
        int dst = pixels[i];
        double da = ((dst >> 24) & 0xFF) / 255.0;

        // Ordinary source-over, in straight alpha. The buffer is handed to Avalonia as
        // Unpremul for exactly this reason: a foil striation is a partially transparent mark
        // over an empty tile, and premultiplying here would darken it toward black.
        double outA = alpha + (da * (1 - alpha));
        if (outA <= 0)
        {
            return;
        }

        double weight = da * (1 - alpha);
        double r = ((color.R * alpha) + (((dst >> 16) & 0xFF) * weight)) / outA;
        double g = ((color.G * alpha) + (((dst >> 8) & 0xFF) * weight)) / outA;
        double b = ((color.B * alpha) + ((dst & 0xFF) * weight)) / outA;

        pixels[i] = Pack(Clamp(r), Clamp(g), Clamp(b), Clamp(outA * 255));
    }

    /// <summary>Moves a colour toward white or black, for a texture's own highlights.</summary>
    private static Color Shift(Color color, int delta) => Color.FromRgb(
        (byte)Clamp(color.R + delta),
        (byte)Clamp(color.G + delta),
        (byte)Clamp(color.B + delta));

    private static int Wrap(int value, int period) => ((value % period) + period) % period;

    private static int Clamp(double value) => value < 0 ? 0 : value > 255 ? 255 : (int)value;

    /// <summary>Packs to the 0xAARRGGBB an Avalonia BGRA8888 buffer expects on little-endian.</summary>
    private static int Pack(int r, int g, int b, int a) => (a << 24) | (r << 16) | (g << 8) | b;

    /// <summary>Copies the buffer into a bitmap, row by row to respect the frame's stride.</summary>
    private static WriteableBitmap ToBitmap(int[] pixels)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(TileSize, TileSize),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using ILockedFramebuffer frame = bitmap.Lock();
        for (int y = 0; y < TileSize; y++)
        {
            Marshal.Copy(pixels, y * TileSize, frame.Address + (y * frame.RowBytes), TileSize);
        }

        return bitmap;
    }
}
