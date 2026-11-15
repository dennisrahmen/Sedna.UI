using Microsoft.Playwright;
using ZXing;
using ZXing.Common;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// Reads a QR code the way a phone does: from pixels, with a decoder the encoder's author did not
/// write (ZXing.Net, a test-only package). The library draws the code in the browser; this asks
/// the page for the picture the download produces and hands ZXing its raw pixels, so a code is
/// proven scannable as shown — with its colours, its shapes, its quiet zone and its logo's hole.
/// </summary>
internal static class QrReader
{
    /// <summary>What ZXing read from raw RGBA pixels, or null when it found no code.</summary>
    public static string? Read(byte[] rgba, int width, int height, bool inverted = false)
    {
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true,
                TryInverted = inverted,
                CharacterSet = "UTF-8",
            },
        };
        var source = new RGBLuminanceSource(rgba, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
        return reader.Decode(source)?.Text;
    }

    /// <summary>
    /// The pixels of the code's own PNG export, as RGBA, at least <paramref name="minimum"/> pixels
    /// square and a whole number of pixels to the module — the size an app should save a code at, since
    /// a module that is not whole pixels is blurred at its edge and a decoder has less to go on.
    /// </summary>
    public static async Task<(byte[] Rgba, int Size)> Pixels(IPage page, string id, int minimum = 480)
    {
        var total = await page.EvaluateAsync<double>("""
            id => {
                const el = document.getElementById(id);
                const quiet = parseFloat(getComputedStyle(el).getPropertyValue('--qr-quiet'));
                return +el.dataset.qrModules + 2 * quiet;
            }
            """, id);
        var size = (int)(Math.Ceiling(minimum / total) * total);
        var base64 = await page.EvaluateAsync<string>("""
            async ([id, size]) => {
                const handle = await sednaUi.qr.get(id);
                const blob = await handle.toBlob({ format: 'png', size });
                const bitmap = await createImageBitmap(blob);
                const canvas = document.createElement('canvas');
                canvas.width = bitmap.width;
                canvas.height = bitmap.height;
                const context = canvas.getContext('2d', { willReadFrequently: true });
                context.drawImage(bitmap, 0, 0);
                const data = context.getImageData(0, 0, canvas.width, canvas.height).data;
                let binary = '';
                for (let i = 0; i < data.length; i += 0x8000) binary += String.fromCharCode.apply(null, data.subarray(i, i + 0x8000));
                return btoa(binary);
            }
            """, new object[] { id, size });
        return (Convert.FromBase64String(base64), size);
    }

    /// <summary>Scans the code with the given id as a camera would.</summary>
    public static async Task<string?> Scan(IPage page, string id, int minimum = 480, bool inverted = false)
    {
        var (rgba, size) = await Pixels(page, id, minimum);
        return Read(rgba, size, size, inverted);
    }

    /// <summary>The pixels of an image given as a data URL, drawn at <paramref name="size"/> pixels square, as RGBA.</summary>
    public static async Task<byte[]> PixelsOfImage(IPage page, string dataUrl, int size = 480)
    {
        var base64 = await page.EvaluateAsync<string>("""
            async ([url, size]) => {
                const image = new Image();
                image.src = url;
                await image.decode();
                const canvas = document.createElement('canvas');
                canvas.width = size;
                canvas.height = size;
                const context = canvas.getContext('2d', { willReadFrequently: true });
                context.drawImage(image, 0, 0, size, size);
                const data = context.getImageData(0, 0, size, size).data;
                let binary = '';
                for (let i = 0; i < data.length; i += 0x8000) binary += String.fromCharCode.apply(null, data.subarray(i, i + 0x8000));
                return btoa(binary);
            }
            """, new object[] { dataUrl, size });
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Wipes a square in the middle of the picture to white, <paramref name="fraction"/> of its side across:
    /// a logo bigger than the one the code left room for, or a thumb over it.
    /// </summary>
    public static void Erase(byte[] rgba, int size, double fraction)
    {
        var side = (int)Math.Round(size * fraction);
        var from = (size - side) / 2;
        for (var y = from; y < from + side; y++)
        {
            for (var x = from; x < from + side; x++)
            {
                var at = ((y * size) + x) * 4;
                rgba[at] = rgba[at + 1] = rgba[at + 2] = 255;
                rgba[at + 3] = 255;
            }
        }
    }
}
