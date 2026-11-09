using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The QR code from markup to symbol, asserted against <c>docs/qr.md</c>: what is drawn decodes to
/// the app's text at the level asked for, in the smallest version, with the mask the standard picks;
/// it matches the matrices of an independent encoder; and it loads only when a page shows one,
/// redraws when its text changes, and keeps dark ink on light paper in every variant.
/// </summary>
/// <remarks>
/// The symbol is read back from the SVG the library drew — one rectangle per run of dark modules,
/// offset by the quiet zone — and decoded by <see cref="QrDecoder"/>, written apart from the
/// encoder. The reference matrices were made by segno 1.6.1 in byte mode, UTF-8, the level fixed
/// and the mask set to the one this encoder chose — segno scores the masks before drawing the
/// format information, this encoder after, and <see cref="QrDecoder"/> checks the choice — with
/// segno's extra zero codeword after a byte-aligned terminator taken out.
/// </remarks>
public class QrTests : ScriptTestBase
{
    private sealed record Vector(string Text, string Level, int Version, int Mask, string Rows);

    private static readonly Vector[] Vectors =
    [
        new("https://example.com/", "L", 2, 6,
            """
            #######.####..#...#######
            #.....#....##...#.#.....#
            #.###.#..#.#.##.#.#.###.#
            #.###.#...##.#..#.#.###.#
            #.###.#...#####.#.#.###.#
            #.....#..#.#..#.#.#.....#
            #######.#.#.#.#.#.#######
            ........##..##.##........
            ##.##.#..#..#...#.#.....#
            #..#.#.##.#..#####.#####.
            .##..##..#..##.###.###..#
            ...###.##.....#..###.####
            ##..#.##.##..####.##....#
            #.###..##.#.#.###...#..#.
            ####..#.#.....###.#.#####
            #..#....#.#.###..###.##.#
            #.#...#...###...#####.##.
            ........####....#...#.##.
            #######..#..###.#.#.#...#
            #.....#..###.#.##...#...#
            #.###.#.####.#.######..##
            #.###.#.###.##..###....##
            #.###.#....#.#.#.#..#####
            #.....#.##.###.#...##.###
            #######.#.####..#.#..#..#
            """),
        new("Straße, Grüße — データ", "Q", 3, 2,
            """
            #######.#..###.#..###.#######
            #.....#...#.#..#.###..#.....#
            #.###.#..#.#..#..##.#.#.###.#
            #.###.#...#...##...##.#.###.#
            #.###.#.####...#..###.#.###.#
            #.....#.##..##.####.#.#.....#
            #######.#.#.#.#.#.#.#.#######
            ...........#####.#.##........
            .#######.#....#...#.#..##...#
            #.###....#.###.##.##.#......#
            ...#.##.####..#######.##..#..
            ##.#.#.#..#..#...#.#..#.#.#.#
            .###..##..#.#.#####....##..#.
            ###..#...###........###..#.##
            #..##.#####...##.##..#.#....#
            ...###..##.###.#.##...#.##.#.
            ..#...#.###..######..##..###.
            #...#..#..#....#.#...#.#.....
            #.#...#..##..#.##..##...#....
            #..#.#.##.#...##.###....#.###
            #.##.###.#.###.#....######..#
            ........####.#..###.#...#..##
            #######.#.##...###.##.#.#.##.
            #.....#.#.#.#...#...#...##...
            #.###.#.#..###....#.#####.#..
            #.###.#.##....###.#..#..##...
            #.###.#.##.#.##..#######...#.
            #.....#.#.......##..#..##..#.
            #######...##.#..#.###.#.#.#..
            """),
        new("a", "H", 1, 0,
            """
            #######.#..##.#######
            #.....#..##.#.#.....#
            #.###.#..#.##.#.###.#
            #.###.#.#.###.#.###.#
            #.###.#.......#.###.#
            #.....#..#.#..#.....#
            #######.#.#.#.#######
            ..........#..........
            ..#.###.#..#.#...#..#
            ..##...#.#...#....##.
            .##.#.#.##.#....#...#
            #..#.#...##.#.#...#..
            ##..#.#.##....#.#.#.#
            ........#.#.##.#.#.##
            #######...#..###.####
            #.....#.####.#####...
            #.###.#.##....##.##.#
            #.###.#...#.###...##.
            #.###.#.#.##.#..#...#
            #.....#...#.###...##.
            #######.....###.#.###
            """),
        new("https://example.com/orders/ORD-4209/proof-of-delivery?parcel=2&signed-by=Alex%20Fischer&at=2026-11-09T08%3A30%3A00Z", "M", 7, 2,
            """
            #######..#....#....#.#.#.#.####..#..#.#######
            #.....#..#.##..#.###.#.##.#....##..#..#.....#
            #.###.#.##..#.###.##...##..###...#.#..#.###.#
            #.###.#.#..#.#.##.##.#####...###...##.#.###.#
            #.###.#.##.####.##.######..##########.#.###.#
            #.....#.#.##.##.#.#.#...###...........#.....#
            #######.#.#.#.#.#.#.#.#.#.#.#.#.#.#.#.#######
            ........#.#..########...#######.#............
            #.#####..###.##...##########.###......#####..
            ...##..#...#...######...##...##....##...#####
            ..###.##.###..###.#....#..#.##.##.######..##.
            ...###.#..#...#....##....#.#..#.#.##.##.#.#..
            ###..###.#.#.#..###.#.##.#...##...#..###.#..#
            #..##..###.###..#.####...#..#.#.#..###...####
            .######.#.#...##.#.##..#####.#.#.###.###.....
            ####.#.####..####.##.#..#.##.##.##.#.######..
            .#....#..###...#..##.....#...###.##...#..#...
            .###.#.####..###.#...#.#.#.#..#....###..#.#.#
            #.#..####.####..##..########......##..#......
            ...###....###.#..###...#....#####..#...##.#.#
            ###########.##..#.#######.....#..#.#######...
            ...##...#..##.#..#.##...#....###.#.##...#####
            #...#.#.###.#.####..#.#.#####..#..#.#.#.#.#..
            .#.##...#..#...#....#...#####...###.#...####.
            .#.######...##...#..#######...##.#..######.##
            ....##.###...##...#.#.##.#..#.##....##.#..#.#
            ..#..###.#..#.#...#.##...##.#....##.#......#.
            #..###..##.#..##..###..##...#.#.#..##.###.###
            #####.##.####.#.#.###...#.##.#.#.##.#.#.##...
            .#..#...#.####........####....####....#...###
            #.###.#####.#..#....##..###..#.####.#..#.#...
            .#####..###.##.#.....##.#...##.#####..##.##.#
            ###...#.####...##....#...###...#....#...#....
            .#.#.#.....#..#....####..#.#####.#.##..#.####
            ....#.###..#.#.#.#....#...##...##.#.##.......
            .####...#.....#.#..######..####.##.####.#.##.
            #..##.#..##.##...##.#####....##..#..#####..##
            ........#.#.##....#.#...##.#.##..#.##...###.#
            #######...##.#.##.###.#.##..#...#.###.#.#.#..
            #.....#.###..##..####...#..####..#.##...###..
            #.###.#.#.#..##.###.#####.#..###...#######.#.
            #.###.#.##....##.##.#.##.#..#.##...#.#.##.###
            #.###.#.####...##.#..#..#.##.#.####.##....##.
            #.....#....##....#..##.#..###.#.#.####....#..
            #######.####.###..#...#.##.#..##...#..####.#.
            """),
    ];

    private const int Quiet = 4;

    private static int MaxBytes(string level) => level switch { "L" => 2953, "M" => 2331, "Q" => 1663, _ => 1273 };

    private static string Code(string id, string text, string level = "", string classes = "qr")
    {
        var levelAttr = level.Length > 0 ? $" data-qr-level=\"{level}\"" : "";
        return $"""<span class="{classes}" id="{id}" data-qr="{System.Net.WebUtility.HtmlEncode(text)}"{levelAttr} data-qr-eager aria-label="Code {id}"></span>""";
    }

    private async Task<IPage> OpenQr(string body) => (await OpenStyled(body)).Page;

    private static Task Ready(IPage page, string id) =>
        page.WaitForFunctionAsync($"() => {{ const el = document.getElementById('{id}'); return el && el.dataset.qrState; }}");

    /// <summary>The module matrix the element's SVG draws, its quiet zone checked and taken off.</summary>
    private static async Task<bool[,]> Matrix(IPage page, string id)
    {
        var drawn = await page.EvaluateAsync<string[]>($$"""
            () => {
                const svg = document.querySelector('#{{id}} > svg');
                return [svg.getAttribute('viewBox') || '', svg.querySelector('path').getAttribute('d') || ''];
            }
            """);
        var box = drawn[0].Split(' ').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(0, box[0]);
        Assert.Equal(0, box[1]);
        Assert.Equal(box[2], box[3]);
        var n = box[2] - (Quiet * 2);
        var m = new bool[n, n];
        var runs = Regex.Matches(drawn[1], @"M(\d+) (\d+)h(\d+)v1h-(\d+)z");
        Assert.Equal(drawn[1].Length, runs.Sum(r => r.Length));
        foreach (Match r in runs)
        {
            var x = int.Parse(r.Groups[1].Value, CultureInfo.InvariantCulture) - Quiet;
            var y = int.Parse(r.Groups[2].Value, CultureInfo.InvariantCulture) - Quiet;
            var w = int.Parse(r.Groups[3].Value, CultureInfo.InvariantCulture);
            Assert.Equal(w, int.Parse(r.Groups[4].Value, CultureInfo.InvariantCulture));
            Assert.True(x >= 0 && y >= 0 && x + w <= n && y < n, $"A dark module is drawn in the quiet zone: {r.Value}");
            for (var i = 0; i < w; i++) m[y, x + i] = true;
        }
        return m;
    }

    private static string Rows(bool[,] m) =>
        string.Join("\n", Enumerable.Range(0, m.GetLength(0)).Select(y =>
            new string(Enumerable.Range(0, m.GetLength(1)).Select(x => m[y, x] ? '#' : '.').ToArray())));

    [Fact]
    public async Task A_page_without_a_code_never_downloads_the_module()
    {
        if (NoBrowser) return;
        var page = await Open("<main><p>No code here.</p></main>");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.qr.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_symbols_match_an_independent_encoder()
    {
        if (NoBrowser) return;
        var page = await OpenQr(string.Concat(Vectors.Select((v, i) => Code($"v{i}", v.Text, v.Level))));

        for (var i = 0; i < Vectors.Length; i++)
        {
            var v = Vectors[i];
            await Ready(page, $"v{i}");
            Assert.Equal(v.Version.ToString(CultureInfo.InvariantCulture), await page.GetAttributeAsync($"#v{i}", "data-qr-version"));
            var m = await Matrix(page, $"v{i}");
            Assert.Equal(v.Rows.ReplaceLineEndings("\n"), Rows(m));
            var decoded = QrDecoder.Decode(m);
            Assert.Equal((v.Text, v.Level[0], v.Version, v.Mask), (decoded.Text, decoded.Level, decoded.Version, decoded.Mask));
        }
    }

    /// <summary>Text of a length in UTF-8 bytes: ASCII, with two- and three-byte characters among it.</summary>
    private static string TextOf(int bytes)
    {
        var text = new StringBuilder();
        var used = 0;
        for (var i = 0; used < bytes; i++)
        {
            var piece = (bytes - used) switch
            {
                >= 3 when i % 11 == 5 => "データ"[i % 3].ToString(),
                >= 2 when i % 7 == 3 => "ß",
                _ => ((char)('!' + ((i * 13) % 90))).ToString(),
            };
            text.Append(piece);
            used += Encoding.UTF8.GetByteCount(piece);
        }
        return text.ToString();
    }

    public static TheoryData<string> Levels => new() { "L", "M", "Q", "H" };

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task Every_size_of_text_decodes_at_every_level(string level)
    {
        if (NoBrowser) return;
        // From one byte to the most the level holds, across every band of versions.
        var max = MaxBytes(level);
        var lengths = new[] { 1, 7, 14, 17, 26, 42, 64, 99, 150, 230, 340, 500, 720, 1000 }
            .Where(l => l < max).Append(max).ToArray();
        var page = await OpenQr(string.Concat(lengths.Select(l => Code($"t{l}", TextOf(l), level))));

        var versions = new List<int>();
        foreach (var l in lengths)
        {
            await Ready(page, $"t{l}");
            Assert.Equal("ready", await page.GetAttributeAsync($"#t{l}", "data-qr-state"));
            var decoded = QrDecoder.Decode(await Matrix(page, $"t{l}"));
            Assert.Equal(TextOf(l), decoded.Text);
            Assert.Equal(level[0], decoded.Level);
            versions.Add(decoded.Version);
        }

        // The smallest version: a longer text never takes a smaller symbol, and the most the level
        // holds takes the largest.
        Assert.Equal(versions.Order(), versions);
        Assert.Equal(40, versions[^1]);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task A_text_too_long_for_the_level_draws_nothing_and_says_so(string level)
    {
        if (NoBrowser) return;
        var page = await OpenQr(Code("q", TextOf(MaxBytes(level) + 1), level));
        await Ready(page, "q");

        Assert.Equal("overflow", await page.GetAttributeAsync("#q", "data-qr-state"));
        Assert.Null(await page.GetAttributeAsync("#q", "data-qr-version"));
        Assert.Null(await page.GetAttributeAsync("#q > svg > path", "d"));
    }

    [Fact]
    public async Task Changing_the_text_or_the_level_draws_the_code_again()
    {
        if (NoBrowser) return;
        var page = await OpenQr(Code("q", "https://example.com/a"));
        await Ready(page, "q");
        Assert.Equal('M', QrDecoder.Decode(await Matrix(page, "q")).Level);

        await Js.Run(page, "() => document.getElementById('q').dataset.qr = 'https://example.com/pair/7Q4K-2HM9'");
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrVersion === '3'");
        Assert.Equal("https://example.com/pair/7Q4K-2HM9", QrDecoder.Decode(await Matrix(page, "q")).Text);

        await Js.Run(page, "() => document.getElementById('q').dataset.qrLevel = 'h'");
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrVersion === '4'");
        var decoded = QrDecoder.Decode(await Matrix(page, "q"));
        Assert.Equal(('H', "https://example.com/pair/7Q4K-2HM9"), (decoded.Level, decoded.Text));

        await Js.Run(page, "() => document.getElementById('q').dataset.qr = ''");
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrState === 'empty'");
        Assert.Null(await page.GetAttributeAsync("#q > svg > path", "d"));
    }

    [Fact]
    public async Task The_code_is_an_image_named_by_the_app()
    {
        if (NoBrowser) return;
        var page = await OpenQr("""<span class="qr" id="q" data-qr="https://example.com/" data-qr-eager aria-label="QR code to the start page"></span>""");
        await Ready(page, "q");

        Assert.Equal("img", await page.GetAttributeAsync("#q", "role"));
        Assert.Equal("QR code to the start page", await page.GetAttributeAsync("#q", "aria-label"));
        Assert.Equal("true", await page.GetAttributeAsync("#q > svg", "aria-hidden"));
        Assert.Equal("", (await page.InnerTextAsync("#q")).Trim());
    }

    [Fact]
    public async Task A_code_is_square_at_every_size_with_dark_ink_on_light_paper_in_both_variants()
    {
        if (NoBrowser) return;
        var page = await OpenQr(
            Code("sm", "https://example.com/", classes: "qr qr--sm")
            + Code("md", "https://example.com/")
            + Code("lg", "https://example.com/", classes: "qr qr--lg")
            + """<span class="qr" id="custom" data-qr="https://example.com/" data-qr-eager style="--qr-size: 123px" aria-label="Code"></span>""");
        foreach (var id in new[] { "sm", "md", "lg", "custom" }) await Ready(page, id);

        var sizes = await page.EvaluateAsync<double[][]>("""
            () => ['sm', 'md', 'lg', 'custom'].map(id => {
                const r = document.getElementById(id).getBoundingClientRect(), s = document.querySelector('#' + id + ' > svg').getBoundingClientRect();
                return [r.width, r.height, s.width, s.height];
            })
            """);
        var rem = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.documentElement).fontSize)");
        var expected = new[] { 6 * rem, 10 * rem, 15 * rem, 123.0 };
        for (var i = 0; i < sizes.Length; i++)
        {
            Assert.Equal(expected[i], sizes[i][0], 1);
            Assert.Equal(expected[i], sizes[i][1], 1);
            Assert.Equal(sizes[i][0], sizes[i][2], 1);
            Assert.Equal(sizes[i][1], sizes[i][3], 1);
        }

        foreach (var variant in new[] { "dark", "light" })
        {
            await Js.Run(page, $"() => document.documentElement.dataset.variant = '{variant}'");
            var colours = await page.EvaluateAsync<double[]>("""
                () => {
                    const lum = c => {
                        const [r, g, b] = c.match(/[\d.]+/g).map(Number);
                        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
                    };
                    const el = document.getElementById('md');
                    return [lum(getComputedStyle(el).backgroundColor), lum(getComputedStyle(el.querySelector('svg')).fill)];
                }
                """);
            Assert.True(colours[0] > 200, $"{Engine}, {variant}: the paper is not light ({colours[0]})");
            Assert.True(colours[1] < 60, $"{Engine}, {variant}: the ink is not dark ({colours[1]})");
        }
    }
}
