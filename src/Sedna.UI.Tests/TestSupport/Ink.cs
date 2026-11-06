using System.IO.Compression;
using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// Where a glyph is actually painted, read from a screenshot: the rows its pixels cover, in
/// page pixels. A box can be centred exactly while the glyph inside it sits a pixel high — an
/// icon font's em box, a line box's leading, an engine's rounding of the font's ascent — and
/// only the paint says where the eye will see it.
/// </summary>
/// <remarks>
/// Taken at four device pixels per CSS pixel. A row counts as painted when a pixel in the
/// element's own box is far from the colour most of that box's pixels have, and an edge row
/// painted only in part says how much of it the edge covers, so a measurement is good to a
/// small fraction of a device pixel.
/// </remarks>
internal static class Ink
{
    public const int Scale = 4;

    /// <summary>
    /// [top, bottom] of the paint of each element, inside its own box, against that box's own
    /// background — the colour most of its pixels have. The page must have been opened at a
    /// device scale factor of <see cref="Scale"/>.
    /// </summary>
    public static async Task<double[][]> Of(IPage page, params string[] selectors)
    {
        var boxes = new List<LocatorBoundingBoxResult>();
        foreach (var selector in selectors)
            boxes.Add((await page.Locator(selector).BoundingBoxAsync())
                ?? throw new InvalidOperationException($"{selector} has no box."));
        var left = boxes.Min(b => b.X) - 1;
        var top = boxes.Min(b => b.Y) - 1;
        var right = boxes.Max(b => b.X + b.Width) + 1;
        var bottom = boxes.Max(b => b.Y + b.Height) + 1;
        var png = await page.ScreenshotAsync(new() { Clip = new() { X = left, Y = top, Width = right - left, Height = bottom - top } });
        var image = Png.Decode(png);

        return boxes.Select(b => Paint(image, left, top, b.X, b.Y, b.Width, b.Height)
            ?? throw new InvalidOperationException("Nothing is painted in that element's box.")).ToArray();
    }

    /// <summary>
    /// [top, bottom] of what is painted inside one box of a screenshot whose top-left corner
    /// sits at (<paramref name="left"/>, <paramref name="top"/>) in page pixels, against the
    /// colour most of the box's pixels have; null when nothing is.
    /// </summary>
    public static double[]? Paint(Png image, double left, double top, double x, double y, double width, double height)
    {
        int x0 = Math.Max(0, (int)Math.Ceiling((x - left) * Scale)), x1 = Math.Min(image.Width, (int)Math.Floor((x + width - left) * Scale));
        int y0 = Math.Max(0, (int)Math.Ceiling((y - top) * Scale)), y1 = Math.Min(image.Height, (int)Math.Floor((y + height - top) * Scale));
        if (x1 <= x0 || y1 <= y0) return null;
        var counts = new Dictionary<(int, int, int), int>();
        for (var py = y0; py < y1; py++)
            for (var px = x0; px < x1; px++)
            {
                var p = image.Pixel(px, py);
                counts[p] = counts.GetValueOrDefault(p) + 1;
            }
        var bg = counts.MaxBy(c => c.Value).Key;

        // How far each row's most-painted pixel is from the background. A horizontal edge that
        // covers part of a device pixel paints that row in proportion, so the edge rows say
        // where inside them the edge falls — what makes this good to a fraction of a pixel.
        var rows = new double[y1 - y0];
        for (var py = y0; py < y1; py++)
            for (var px = x0; px < x1; px++)
            {
                var p = image.Pixel(px, py);
                var d = Math.Abs(p.R - bg.Item1) + Math.Abs(p.G - bg.Item2) + Math.Abs(p.B - bg.Item3);
                if (d > rows[py - y0]) rows[py - y0] = d;
            }
        var full = rows.Max();
        if (full < 96) return null;
        var floor = full * 0.15;
        var first = Array.FindIndex(rows, d => d > floor);
        var last = Array.FindLastIndex(rows, d => d > floor);
        var topEdge = first + 1 - Math.Min(1, rows[first] / full);
        var bottomEdge = last + Math.Min(1, rows[last] / full);
        return [top + (y0 + topEdge) / Scale, top + (y0 + bottomEdge) / Scale];
    }

    public static double Centre(double[] ink) => (ink[0] + ink[1]) / 2;
    /// <summary>A PNG decoder for Playwright's own screenshots: 8-bit RGB or RGBA, not interlaced.</summary>
    public sealed class Png
    {
        public int Width { get; private init; }
        public int Height { get; private init; }
        private byte[] Data { get; init; } = [];
        private int Bpp { get; init; }

        public (int R, int G, int B) Pixel(int x, int y)
        {
            var o = (y * Width + x) * Bpp;
            return (Data[o], Data[o + 1], Data[o + 2]);
        }

        public static Png Decode(byte[] bytes)
        {
            int p = 8, w = 0, h = 0, colourType = 6;
            using var idat = new MemoryStream();
            while (p < bytes.Length)
            {
                var len = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
                var type = System.Text.Encoding.ASCII.GetString(bytes, p + 4, 4);
                if (type == "IHDR")
                {
                    w = (bytes[p + 8] << 24) | (bytes[p + 9] << 16) | (bytes[p + 10] << 8) | bytes[p + 11];
                    h = (bytes[p + 12] << 24) | (bytes[p + 13] << 16) | (bytes[p + 14] << 8) | bytes[p + 15];
                    if (bytes[p + 16] != 8) throw new NotSupportedException("Only 8-bit PNGs.");
                    colourType = bytes[p + 17];
                }
                if (type == "IDAT") idat.Write(bytes, p + 8, len);
                p += 12 + len;
            }
            var bpp = colourType == 2 ? 3 : 4;
            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            z.CopyTo(raw);
            var src = raw.ToArray();
            var stride = w * bpp;
            var data = new byte[h * stride];
            for (var y = 0; y < h; y++)
            {
                var filter = src[y * (stride + 1)];
                for (var x = 0; x < stride; x++)
                {
                    int a = x >= bpp ? data[y * stride + x - bpp] : 0;
                    int b = y > 0 ? data[(y - 1) * stride + x] : 0;
                    int c = x >= bpp && y > 0 ? data[(y - 1) * stride + x - bpp] : 0;
                    int v = src[y * (stride + 1) + 1 + x];
                    v += filter switch
                    {
                        1 => a,
                        2 => b,
                        3 => (a + b) >> 1,
                        4 => Paeth(a, b, c),
                        _ => 0,
                    };
                    data[y * stride + x] = (byte)v;
                }
            }
            return new Png { Width = w, Height = h, Data = data, Bpp = bpp };
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
