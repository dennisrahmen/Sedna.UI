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

    [Fact]
    public void Only_the_frame_itself_sets_how_a_frame_lays_out()
    {
        // `.media-open > .media-frame { display: block }` took the frame's centring away
        // wherever a picture opened large, so a `.file-icon` in a gallery tile sat in the
        // corner and the tile became a link to avoid it. What holds a frame says nothing
        // about how the frame lays out what it holds.
        var css = Assets.StripComments(Assets.Css);
        var rules = Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])(?:display|align-items|justify-content)\s*:"))
            .SelectMany(m => m.Groups["sel"].Value.Split(',').Select(Assets.Squash))
            .Where(sel => Regex.IsMatch(sel, @"\.media-frame(?![\w-])\s*$") && sel != ".media-frame")
            .ToList();

        Assert.True(rules.Count == 0, "These change how a .media-frame lays out what it holds: " + string.Join(" | ", rules));
    }

    [Fact]
    public async Task A_frame_centres_a_file_icon_wherever_it_is()
    {
        if (NoBrowser) return;
        const string Icon = """<span class="media-frame" data-f><span class="file-icon file-icon--lg file-icon--pdf"><i class="ri-file-pdf-2-line"></i></span></span>""";
        var (page, errors) = await OpenStyled($"""
            <div style="width:480px">
              <div data-in="alone">{Icon}</div>
              <button class="media-open" type="button" data-in="media-open">{Icon}</button>
              <div class="gallery">
                <button class="media-open" type="button" data-in="gallery button">{Icon}</button>
                <a href="#" data-in="gallery link">{Icon}</a>
              </div>
              <div class="gallery gallery--tight">
                <button class="media-open" type="button" data-in="tight gallery">{Icon}</button>
              </div>
            </div>
            """);

        var offsets = await page.EvaluateAsync<Dictionary<string, double>>("""
            () => Object.fromEntries([...document.querySelectorAll('[data-in]')].map(holder => {
                const f = holder.querySelector('[data-f]').getBoundingClientRect();
                const i = holder.querySelector('.file-icon').getBoundingClientRect();
                return [holder.dataset.in, Math.max(
                    Math.abs((f.left + f.right) / 2 - (i.left + i.right) / 2),
                    Math.abs((f.top + f.bottom) / 2 - (i.top + i.bottom) / 2))];
            }))
            """);

        Assert.Equal(5, offsets.Count);
        foreach (var (holder, off) in offsets)
            Assert.True(off < 1, $"{Engine}: in {holder}, the file icon is {off:0.0}px off the frame's centre.");
        Assert.Empty(errors);
    }
}
