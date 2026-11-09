using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every frame that holds a picture reads one ratio token, <c>--media-ratio</c>, and takes one
/// modifier, <c>--transparency</c> (62-media-and-prose.css).
/// </summary>
/// <remarks>
/// The ratio was written five times as five literals, two of them disagreeing: a 16:9
/// <c>.skeleton-image</c> swapped for its loaded 16:10 <c>.media-frame</c> changed height. Both
/// checks below find their members in the stylesheet, so a frame added later is held at once.
/// </remarks>
public class MediaFrameTests : ScriptTestBase
{
    /// <summary>
    /// A box at a fixed ratio that is not a picture's frame: a calendar day, and a signature pad,
    /// whose ratio is the box its value is written in and has a token of its own.
    /// </summary>
    private static readonly string[] NotAFrame = ["cal-day", "form-signature-pad"];

    /// <summary>A picture that is the thing itself — a face, a mark, an icon — never one being checked.</summary>
    private static readonly string[] NotAnUpload = ["avatar", "brand-logo", "file-icon", "link-preview"];

    [Fact]
    public void Every_fixed_ratio_reads_the_ratio_token()
    {
        var css = Assets.StripComments(Assets.Css);
        var literal = Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])aspect-ratio\s*:(?!\s*(?:var\(--media-ratio|auto))"))
            .Select(m => Assets.Squash(m.Groups["sel"].Value))
            .Where(sel => !NotAFrame.Any(c => sel.Contains("." + c, StringComparison.Ordinal)))
            .ToList();

        Assert.True(literal.Count == 0,
            "These set a ratio of their own instead of reading --media-ratio, so an app cannot reach them: "
            + string.Join(" | ", literal));
    }

    [Fact]
    public void Every_picture_frame_takes_the_transparency_modifier()
    {
        var css = Assets.StripComments(Assets.Css);
        var frames = Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}")
            .Where(m => m.Groups["body"].Value.Contains("object-fit", StringComparison.Ordinal))
            .SelectMany(m => m.Groups["sel"].Value.Split(','))
            .Select(s => Regex.Match(s, @"\.(?<c>[a-z][a-z0-9-]*?)(?:--[a-z0-9-]+)?\s*>\s*(?::is\([^)]*\bimg\b|img\b)"))
            .Where(m => m.Success)
            .Select(m => m.Groups["c"].Value)
            .Except(NotAnUpload)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("media-frame", frames);
        var missing = frames.Where(f => !css.Contains($".{f}--transparency", StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "These frame a picture and have no --transparency modifier: ." + string.Join(", .", missing));
    }

    private const string Frames = """
        <div style="width:320px">
          <div class="media-frame" data-f="media-frame"></div>
          <div class="skeleton skeleton-image" data-f="skeleton-image"></div>
          <label class="choice-tile"><input type="radio" name="r"><span class="choice-tile-art" data-f="choice-tile-art"></span></label>
          <figure class="media-player"><video data-f="media-player video"></video></figure>
          <div class="file-grid"><div class="file-tile"><div class="file-tile-thumb" data-f="file-tile-thumb"></div></div></div>
        </div>
        """;

    private static readonly string Ratios = """
        root => Object.fromEntries([...document.querySelectorAll(root + ' [data-f]')].map(el => {
            const r = el.getBoundingClientRect();
            return [el.dataset.f, r.width / r.height];
        }))
        """;

    [Fact]
    public async Task Every_frame_defaults_to_its_ratio_and_a_container_sets_them_all()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div id="plain">{Frames}</div>
            <div id="set" style="--media-ratio: 4 / 3">{Frames}</div>
            <div style="width:320px">
              <div class="media-frame media-frame--square" id="square"></div>
              <div class="media-frame media-frame--wide" id="wide"></div>
              <div class="gallery"><a href="#"><span class="media-frame" id="gallery"></span></a></div>
              <div class="file-grid file-grid--wide"><div class="file-tile"><div class="file-tile-thumb" id="wide-thumb"></div></div></div>
            </div>
            """);

        var plain = await page.EvaluateAsync<Dictionary<string, double>>(Ratios, "#plain");
        var set = await page.EvaluateAsync<Dictionary<string, double>>(Ratios, "#set");

        Assert.Equal(5, plain.Count);
        foreach (var (frame, ratio) in plain)
            Assert.True(Math.Abs(ratio - (frame == "file-tile-thumb" ? 1 : 16.0 / 9)) < 0.02, $"{Engine}: .{frame} is {ratio:0.000}:1 by default.");
        foreach (var (frame, ratio) in set)
            Assert.True(Math.Abs(ratio - 4.0 / 3) < 0.02, $"{Engine}: .{frame} is {ratio:0.000}:1 under --media-ratio: 4 / 3.");

        var named = await page.EvaluateAsync<double[]>("""
            () => ['square', 'wide', 'gallery', 'wide-thumb'].map(id => {
                const r = document.getElementById(id).getBoundingClientRect();
                return r.width / r.height;
            })
            """);
        Assert.Equal(1, named[0], 0.02);
        Assert.Equal(21.0 / 9, named[1], 0.02);
        Assert.Equal(1, named[2], 0.02);
        Assert.Equal(16.0 / 9, named[3], 0.02);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_transparency_frame_paints_a_checkerboard_behind_the_picture_alone()
    {
        if (NoBrowser) return;
        const string Pixel = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
        var (page, errors) = await OpenStyled($"""
            <div style="width:320px">
              <div class="media-frame media-frame--transparency" data-f><img src="{Pixel}" width="80" height="40" alt=""></div>
              <div class="file-tile-thumb file-tile-thumb--transparency" data-f><img src="{Pixel}" alt=""></div>
              <span class="file-thumb file-thumb--transparency" data-f><img src="{Pixel}" alt=""></span>
              <span class="choice-tile-art choice-tile-art--transparency" data-f><img src="{Pixel}" alt=""></span>
              <div class="lightbox-stage lightbox-stage--transparency" data-f style="position:static; height:200px"><img src="{Pixel}" alt=""></div>
              <div class="media-frame" id="plain"><img src="{Pixel}" alt=""></div>
            </div>
            """);

        var painted = await page.EvaluateAsync<string[][]>("""
            () => [...document.querySelectorAll('[data-f]'), document.getElementById('plain')].map(f => [
                f.className,
                getComputedStyle(f.querySelector('img')).backgroundImage,
                getComputedStyle(f).backgroundImage])
            """);

        foreach (var row in painted[..^1])
        {
            Assert.True(row[1].Contains("conic-gradient", StringComparison.Ordinal), $"{Engine}: .{row[0]} paints no checkerboard behind its picture.");
            Assert.Equal("none", row[2]);   // behind the picture, never the letterbox
        }
        Assert.Equal("none", painted[^1][1]);
        Assert.Empty(errors);
    }
}
