using System.Drawing;
using Microsoft.Playwright;
using Xunit.Abstractions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Everything the catalogue shows sits on its centre line: in every row that centres its
/// items, each item is painted on one line, and a box that holds one line of text paints it
/// in its middle — on every page, in every density and in both directions.
/// </summary>
/// <remarks>
/// <para>
/// Measured on the paint, read from screenshots at four device pixels per CSS pixel, because
/// the boxes can all be centred while what is drawn in them is not: a glyph sits where its
/// font puts it inside its line, an icon font and a text font differently.
/// </para>
/// <para>
/// Before measuring, every letter on the page is drawn as a capital H and every icon as a
/// filled circle. A word's own shape — a descender, a lowercase line — and an icon's own
/// drawing are what the designer chose; the line they are set on is what the library
/// chooses, and that is what this holds.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
[Trait(ScanKind.Name, ScanKind.Scan)]
public class AlignmentScanTests(CatalogueAppFixture app, ITestOutputHelper output)
{
    /// <summary>
    /// How far from its centre line a drawing may land: a pixel. Both engines trim a line to
    /// its font's cap height rounded to a pixel and draw the glyph at the exact one, place a
    /// table cell's content on a whole pixel, and set a line of text on a whole pixel where its
    /// box falls between pixels — Chromium always, WebKit on Linux and in some layouts on
    /// Apple's platforms — so a word can land up to a pixel from where the rules put it.
    /// </summary>
    private static double Tolerance => 1.0 + Measured;

    /// <summary>What the measurement itself is good to: a sixteenth of a pixel.</summary>
    private const double Measured = 1.0 / 16;

    /// <summary>The variants one page is measured in: its density and its direction.</summary>
    private static readonly (string Density, string Dir)[] VariantList =
        [.. from dir in new[] { "ltr", "rtl" } from density in new[] { "comfortable", "compact", "touch" } select (density, dir)];

    /// <summary>
    /// One case per page, so each lasts seconds and a failure names its page: the whole
    /// catalogue in one case outran the test host's hang timeout on a slow runner. The page is
    /// loaded once and measured in every variant, set on it in turn.
    /// </summary>
    public static TheoryData<string> Pages() => RoutedPages.ScannedAsTheoryData();

    private sealed record Box(string Kind, string Label, double X, double Y, double W, double H, int K, int[]? Fg = null, double[]? Line = null)
    {
        /// <summary>The same box where its element stands now, <paramref name="dy"/> lower.</summary>
        public Box Shifted(double dy) => this with { Y = Y + dy, Line = Line is [var t, var b] ? [t + dy, b + dy] : null };
    }
    private sealed record Row(string Path, double Centre, Box[] Items);
    private sealed record Scan(Row[] Rows, Box[] Boxes, string[] Bare, double Height);

    /// <summary>How often a page moved while it was measured and was measured again, over the run.</summary>
    private static int _remeasured;

    private static void CountMeasuredAgain() => Interlocked.Increment(ref _remeasured);

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Every_row_and_every_box_paints_on_its_centre_line(string route)
    {
        if (app.NoBrowser || route.Length == 0) return;

        var context = await app.Browser!.NewContextAsync(new()
        {
            DeviceScaleFactor = Ink.Scale,
            ViewportSize = new() { Width = 1280, Height = 900 },
            ReducedMotion = ReducedMotion.Reduce,
        });
        var problems = new List<string>();
        var timing = new List<string>();
        try
        {
            var page = await context.NewPageAsync();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await Open(page, route);
            timing.Add($"load {clock.ElapsedMilliseconds}ms");
            foreach (var (density, dir) in VariantList)
            {
                // A page that moves while it is measured — a render that lands between reading
                // the positions and taking the picture — is loaded and measured again from the start.
                List<string>? found = null;
                string? moved = null;
                for (var attempt = 0; attempt < 3 && found is null; attempt++)
                {
                    if (attempt > 0)
                    {
                        CountMeasuredAgain();
                        await Open(page, route);
                    }
                    clock.Restart();
                    // Back at the top, as a fresh page is: the scan reads where things are from there.
                    await Js.Run(page, "() => window.scrollTo(0, 0)");
                    var scan = System.Text.Json.JsonSerializer.Deserialize<Scan>(
                        await page.EvaluateAsync<string>(Scanner, new { density, dir }),
                        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
                    await Js.Settle(page);
                    var scanned = clock.ElapsedMilliseconds;
                    (found, moved) = await Measure(page, scan, route);
                    timing.Add($"{density}/{dir} scan {scanned}ms measure {clock.ElapsedMilliseconds - scanned}ms{(attempt > 0 ? $" (attempt {attempt + 1})" : "")}");
                    if (found is not null) found.InsertRange(0, scan.Bare.Select(b => $"{route} {b}: a bare word beside an element"));
                }

                if (found is null) problems.Add($"{route}: the page kept moving while it was measured — {moved}");
                else problems.AddRange(found.Select(p => $"[{density} {dir}] {p}"));
            }
            await page.CloseAsync();
        }
        finally { await context.CloseAsync(); }

        output.WriteLine($"{route}: {string.Join("; ", timing)}; pages measured again so far: {_remeasured}");
        var report = string.Join(Environment.NewLine, problems);
        Assert.True(problems.Count == 0, $"{problems.Count} off their centre line:{Environment.NewLine}{report}");
    }

    /// <summary>
    /// The circuit is connected before the scan: its first render replaces what the server
    /// rendered, so a scan before it has its letters turned back from H into the page's words —
    /// descenders and all — under the centre lines it recorded for the H's. A heavy page
    /// connects late enough for that to land mid-scan. The fonts are awaited by the scan
    /// itself, and a layout that still moves is caught by the measurement.
    /// </summary>
    private Task Open(IPage page, string route) => app.GotoInteractiveAsync(page, route);

    /// <summary>
    /// Where every measured element stands now: whether it is wholly in the viewport with
    /// nothing over it, and how far it has moved from where the scan read it — a sticky
    /// element moves with the scroll, and is measured where it is.
    /// </summary>
    private sealed record Where(double Y, double[][] Items);

    private const string Band = """
        () => {
          const nodes = window.__alignNodes, at = window.__alignAt, pinned = window.__alignPinned;
          // The middle and four points near the corners: something over only part of an item —
          // a figure hanging over a row, a sticky bar over a scrolled one — still changes what is
          // drawn there.
          const points = [[0.5, 0.5], [0.2, 0.2], [0.8, 0.2], [0.2, 0.8], [0.8, 0.8]];
          // Wholly inside every scroller around it: a row cut by a scroller's edge a few pixels
          // from its bottom still passes the five points, and its picture is a cut one.
          const unclipped = (n, r) => {
            for (let e = n.parentElement; e && e !== document.body; e = e.parentElement) {
              const cs = getComputedStyle(e);
              if (cs.overflowX === 'visible' && cs.overflowY === 'visible') continue;
              const c = e.getBoundingClientRect();
              const top = c.top + e.clientTop, left = c.left + e.clientLeft;
              if (r.top < top - 0.5 || r.bottom > top + e.clientHeight + 0.5 || r.left < left - 0.5 || r.right > left + e.clientWidth + 0.5) return false;
            }
            return true;
          };
          return JSON.stringify({ y: scrollY, items: nodes.map((n, k) => {
            const r = n.getBoundingClientRect();
            const inView = r.top >= 0 && r.bottom <= innerHeight && r.left >= 0 && r.right <= innerWidth && unclipped(n, r);
            const clear = inView && points.every(([fx, fy]) => {
              const hit = document.elementFromPoint(r.left + r.width * fx, r.top + r.height * fy);
              return !!hit && (n.contains(hit) || hit.contains(n));
            });
            return [clear ? 1 : 0, r.top + scrollY - at[k], pinned[k]];
          }) });
        }
        """;

    /// <summary>
    /// Measured a viewport at a time, scrolled down the page: the viewport stays the size a
    /// reader has, so nothing sized by it moves, and no picture is larger than one screen —
    /// a whole tall page at four pixels to the pixel is more than a loaded runner's WebKit
    /// holds. Each element is measured in the first band that shows all of it, uncovered.
    /// </summary>
    /// <summary>What was found, or what moved while it was being measured.</summary>
    private sealed record Outcome(List<string>? Found, string? Moved);

    /// <summary>
    /// Decoding a picture and reading the paint out of it is the longest step of a band, and
    /// takes the CPU and not the browser: it runs beside the browser's next band, and a few at
    /// a time, because a decoded screenshot is tens of megabytes.
    /// </summary>
    private static readonly SemaphoreSlim Decoding = new(Math.Max(1, Environment.ProcessorCount / 2));

    private static async Task<Outcome> Measure(IPage page, Scan scan, string route)
    {
        var rows = scan.Rows.ToList();
        var boxes = scan.Boxes.ToList();
        var viewport = page.ViewportSize!.Height;
        var width = page.ViewportSize!.Width;
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        // One job per band: what the band's picture says about the rows and boxes it shows.
        var bands = new List<Task<List<string>>>();
        try
        {
            for (double top = 0; rows.Count > 0 || boxes.Count > 0; top += viewport - 300)
            {
                await Js.Run(page, "y => window.scrollTo(0, y)", top);
                await Js.Frames(page);
                async Task<Where> Read() => System.Text.Json.JsonSerializer.Deserialize<Where>(await page.EvaluateAsync<string>(Band), options)!;
                // The part of the viewport that holds what can be measured in this band, to the
                // pixel so a clipped picture is the same pixels as the whole one: nothing else
                // is read, and a picture is decoded in proportion to its area.
                RectangleF ClipOf(Where w)
                {
                    double x0 = width, y0 = viewport, x1 = 0, y1 = 0;
                    foreach (var b in rows.SelectMany(r => r.Items).Concat(boxes).Where(b => w.Items[b.K][0] > 0))
                    {
                        var at = b.Shifted(w.Items[b.K][1]);
                        (x0, y0, x1, y1) = (Math.Min(x0, at.X), Math.Min(y0, at.Y - w.Y), Math.Max(x1, at.X + at.W), Math.Max(y1, at.Y - w.Y + at.H));
                    }
                    x0 = Math.Max(0, Math.Floor(x0) - 4); y0 = Math.Max(0, Math.Floor(y0) - 4);
                    x1 = Math.Min(width, Math.Ceiling(x1) + 4); y1 = Math.Min(viewport, Math.Ceiling(y1) + 4);
                    return x1 > x0 && y1 > y0 ? new((float)x0, (float)y0, (float)(x1 - x0), (float)(y1 - y0)) : new(0, 0, width, viewport);
                }
                async Task<byte[]> Shoot(Where w)
                {
                    var c = ClipOf(w);
                    return await page.ScreenshotAsync(new() { Clip = new() { X = c.X, Y = c.Y, Width = c.Width, Height = c.Height } });
                }
                // Taken again while the page moves between reading where things are and the picture,
                // so what is measured is the page as it stays. A pinned element that moved is
                // measured in a later band: WebKit moves one pinned to the viewport's bottom while it
                // takes the picture.
                var before = await Read();
                var clip = ClipOf(before);
                var shot = await Shoot(before);
                var after = await Read();
                double[] shift = [];
                (double d, int k) moved = default;
                for (var take = 0; ; take++)
                {
                    shift = before.Items.Zip(after.Items, (b, a) => Math.Abs(b[1] - a[1])).ToArray();
                    moved = shift.Select((d, k) => (d, k)).Where(x => before.Items[x.k][2] < 0.5).DefaultIfEmpty().MaxBy(x => x.d);
                    if (moved.d <= 0.1) break;
                    if (take == 2)
                    {
                        var what = scan.Rows.SelectMany(r => r.Items).Concat(scan.Boxes).First(b => b.K == moved.k);
                        return new(null, $"{what.Label} moved {moved.d:0.00}px");
                    }
                    before = after;
                    clip = ClipOf(before);
                    shot = await Shoot(before);
                    after = await Read();
                }

                bool Clear(Box b) => before.Items[b.K][0] > 0 && shift[b.K] <= 0.1;
                var origin = before.Y;
                var here = before;
                var picked = rows.Where(r => r.Items.All(Clear)).ToList();
                rows.RemoveAll(picked.Contains);
                var boxed = boxes.Where(Clear).ToList();
                boxes.RemoveAll(boxed.Contains);
                bands.Add(Task.Run(async () =>
                {
                    await Decoding.WaitAsync();
                    try { return Judge(Ink.Png.Decode(shot), clip.X, origin + clip.Y, here, picked, boxed, route); }
                    finally { Decoding.Release(); }
                }));

                // The last band: what was never wholly shown uncovered is not on screen to measure.
                if (origin + viewport >= scan.Height - 1) break;
            }
            return new((await Task.WhenAll(bands)).SelectMany(p => p).ToList(), null);
        }
        finally
        {
            // A band still being read is waited for, so none runs on after its test has ended.
            foreach (var band in bands) await Task.WhenAny(band);
        }
    }

    /// <summary>What one band's picture says: the rows whose items are off their centre line and the boxes whose text is off its middle.</summary>
    private static List<string> Judge(Ink.Png image, double left, double top, Where at, List<Row> rows, List<Box> boxes, string route)
    {
        var problems = new List<string>();
        Box Now(Box b) => b.Shifted(at.Items[b.K][1]);
        foreach (var row in rows)
        {
            var items = row.Items.Select(Now).ToList();
            var centre = row.Centre + at.Items[row.Items[0].K][1];
            // A word and an icon are measured on what is drawn; anything else — an avatar, a
            // ring, a pill — on its box, since what it draws inside is its own design.
            var centres = items
                .Select(i => (Item: i, Ink: i.Kind == "box" ? [i.Y, i.Y + i.H] : Paint(image, left, top, i)))
                .Where(m => m.Ink is not null)
                .Select(m => (m.Item, Centre: Ink.Centre(m.Ink!)))
                .ToList();
            if (centres.Count < 2) continue;
            var off = centres.Where(c => Math.Abs(c.Centre - centre) > Tolerance).ToList();
            if (off.Count > 0)
                problems.Add($"{route} {row.Path}: "
                    + string.Join(", ", off.Select(c => $"{c.Item.Kind} {c.Item.Label} {c.Centre - centre:+0.00;-0.00} at ({c.Item.X:0}, {c.Item.Y:0})"))
                    + " from the row's centre line");
        }

        foreach (var box in boxes)
        {
            var now = Now(box);
            var ink = Paint(image, left, top, now);
            if (ink is null) continue;
            var delta = Ink.Centre(ink) - (now.Y + now.H / 2);
            if (Math.Abs(delta) > Tolerance)
                problems.Add($"{route} {box.Label}: its text is {delta:+0.00;-0.00}px from the middle of its box");
        }
        return problems;
    }

    /// <summary>
    /// What is drawn in an item: a pixel in from its top and bottom, where its own
    /// antialiased edge is, within its word's line, and across the middle three fifths of
    /// the word, clear of the curve of a rounded end — a pill's radius reaches further in
    /// than its padding — and counting only the word's own colour.
    /// </summary>
    private static double[]? Paint(Ink.Png image, double left, double top, Box b)
    {
        // Within the word's own line, as layout placed it, and two pixels either side: what is
        // drawn there is the word, and the box's middle is still what it is measured against.
        var y0 = b.Y + 1;
        var y1 = b.Y + b.H - 1;
        if (b.Line is [var lt, var lb]) (y0, y1) = (Math.Max(y0, lt - 2), Math.Min(y1, lb + 2));
        return Ink.Paint(image, left, top, b.X + b.W * 0.2, y0, b.W * 0.6, y1 - y0,
            b.Fg is { Length: 3 } c ? (c[0], c[1], c[2]) : null);
    }

    /// <summary>
    /// Sets the variant, draws every letter as H and every icon as a circle, and returns the
    /// rows that centre their items and the boxes that hold one line of text, in page pixels.
    /// </summary>
    private const string Scanner = """
        async ({ density, dir }) => {
          const root = document.documentElement;
          if (density === 'comfortable') root.removeAttribute('data-density'); else root.setAttribute('data-density', density);
          root.setAttribute('dir', dir);
          // The scan's own styles from an earlier variant on this page are replaced, not stacked.
          document.querySelectorAll('style[data-align-scan]').forEach(e => e.remove());
          const style = document.createElement('style');
          style.setAttribute('data-align-scan', '');
          // No scrollbar while the page is measured: WebKit takes its picture without one, and a
          // layout that fits what the width allows — a year of small months — reflows when it goes.
          // No hover hint either: scrolling under a resting pointer hovers whatever passes under
          // it, and the hint it shows a moment later is drawn over what is being measured.
          style.textContent = '[class^="ri-"]::before, [class*=" ri-"]::before { content: "\\eb7c" !important; } *, *::before, *::after { caret-color: transparent !important; } html { scrollbar-width: none !important; } ::-webkit-scrollbar { display: none !important; } .sedna-tip { display: none !important; }';
          document.head.appendChild(style);
          const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
          for (let n = walker.nextNode(); n; n = walker.nextNode()) {
            if (n.parentElement && n.parentElement.closest('script, style, pre, code, textarea, svg')) continue;
            n.data = n.data.replace(/\S/g, 'H');
          }
          for (const input of document.querySelectorAll('input:not([type]), input[type="text"], input[type="search"], input[type="email"], input[type="number"], input[type="url"], input[type="tel"], input[type="password"]')) {
            if (input.value) input.value = input.value.replace(/\S/g, 'H');
            if (input.placeholder) input.placeholder = input.placeholder.replace(/\S/g, 'H');
          }
          // A label Razor writes in pieces — `v@(Version) · main` — is one word on screen.
          document.body.normalize();
          await document.fonts.ready;
          await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));

          const sy = window.scrollY, sx = window.scrollX;
          // The colour a word is drawn in, as [r, g, b], when it is a plain opaque one.
          const ink = el => { const m = /^rgba?\((\d+), (\d+), (\d+)(?:, ([\d.]+))?\)$/.exec(getComputedStyle(el).color);
            return m && (m[4] === undefined || parseFloat(m[4]) === 1) ? [+m[1], +m[2], +m[3]] : null; };
          const box = (r, kind, label, node) => { const b = { kind, label, x: r.left + sx, y: r.top + sy, w: r.width, h: r.height,
            fg: kind === 'text' || kind === 'bare' || kind === 'box' && node && !node.children?.length ? ink(node) : null };
            Object.defineProperty(b, 'node', { value: node, enumerable: false }); return b; };
          const name = el => el.tagName.toLowerCase() + (el.classList.length ? '.' + [...el.classList].join('.') : '');
          const path = el => {
            const parts = [];
            for (let e = el; e && e !== document.body && parts.length < 3; e = e.parentElement)
              if (e === el || e.classList.length) parts.unshift(name(e));
            const anchor = el.closest('[id]');
            return (anchor ? '#' + anchor.id + ' ' : '') + parts.join(' > ');
          };
          const visible = el => {
            if (!el.getClientRects().length) return false;
            const cs = getComputedStyle(el);
            return cs.visibility !== 'hidden' && cs.opacity !== '0';
          };
          const isIcon = el => [...el.classList].some(c => c.startsWith('ri-'));
          const painted = cs => (cs.backgroundColor !== 'rgba(0, 0, 0, 0)' && cs.backgroundColor !== 'transparent')
            || ['Top', 'Bottom'].some(s => parseFloat(cs['border' + s + 'Width']) > 0 && cs['border' + s + 'Color'] !== 'rgba(0, 0, 0, 0)');
          const oneLine = n => {
            const r = document.createRange(); r.selectNodeContents(n);
            const rects = [...r.getClientRects()].filter(x => x.width > 0);
            if (!rects.length) return null;
            const top = Math.min(...rects.map(x => x.top)), bottom = Math.max(...rects.map(x => x.bottom));
            return rects.every(x => Math.abs(x.top - rects[0].top) < 1) ? r.getBoundingClientRect() : null;
          };

          const rows = [], boxes = [], bare = [];
          for (const el of document.body.querySelectorAll('*')) {
            if (el.closest('pre, svg, script, style, template, .cat-code')) continue;
            const cs = getComputedStyle(el);
            if (!visible(el)) continue;
            const flexRow = cs.display.includes('flex') && cs.flexDirection.startsWith('row') && cs.flexWrap === 'nowrap';

            if (flexRow && cs.alignItems === 'center') {
              const items = [];
              let skip = false;
              for (const n of el.childNodes) {
                if (n.nodeType === Node.TEXT_NODE) {
                  if (!n.data.trim()) continue;
                  const r = oneLine(n);
                  if (!r) { skip = true; break; }
                  const it = box(r, 'bare', '"' + n.data.trim().length + 'ch"', n.parentElement);
                  it.line = [r.top + sy, r.bottom + sy];
                  items.push(it);
                } else if (n.nodeType === Node.ELEMENT_NODE) {
                  if (!visible(n)) continue;
                  const c = getComputedStyle(n);
                  if (c.position === 'absolute' || c.position === 'fixed') continue;
                  if (c.alignSelf !== 'auto' && c.alignSelf !== 'center' && c.alignSelf !== 'normal') continue;
                  // An item the stylesheet moves off the line by a relative inset is set there on
                  // purpose — a rating's figure sits on the stars' weight, below their middle — and
                  // its own test measures where it goes.
                  if (c.position === 'relative' && (parseFloat(c.top) || 0) !== 0) continue;
                  const r = n.getBoundingClientRect();
                  if (r.width < 1 || r.height < 1) continue;
                  const textOnly = !n.children.length && n.textContent.trim();
                  const line = textOnly ? oneLine(n) : null;
                  if (textOnly && !line) { skip = true; break; }
                  // A word is measured across its own width, so a separator or a marker its
                  // element draws beside it is not taken for it.
                  // Within its own box: a word cut short with an ellipsis runs past it, invisibly.
                  const l = line ? Math.max(line.left, r.left) : 0, rr = line ? Math.min(line.right, r.right) : 0;
                  // Inside its border, so a painted item's own edge is not taken for its word.
                  const ibt = parseFloat(c.borderTopWidth) || 0, ibb = parseFloat(c.borderBottomWidth) || 0;
                  const across = line && rr - l >= 1
                    ? { left: l, top: r.top + ibt, width: rr - l, height: r.height - ibt - ibb }
                    : r;
                  const it = box(across, isIcon(n) ? 'icon' : textOnly ? 'text' : 'box', name(n), n);
                  if (line) it.line = [line.top + sy, line.bottom + sy];
                  items.push(it);
                }
              }
              // A painted row is seen as the area inside its borders — a border on one side is a
              // divider or an indicator, not part of the field — so its items belong in the
              // middle of that, padding and all; an unpainted one only where it puts its content.
              const rb = el.getBoundingClientRect();
              const bt = parseFloat(cs.borderTopWidth), bb = parseFloat(cs.borderBottomWidth);
              const centre = painted(cs) ? rb.top + sy + (bt + rb.height - bb) / 2
                : rb.top + sy + (bt + parseFloat(cs.paddingTop) + rb.height - bb - parseFloat(cs.paddingBottom)) / 2;
              if (!skip && items.length >= 2 && items.some(i => i.kind !== 'box')) rows.push({ path: path(el), centre, items });
              // A word that is the row's own text node beside an element cannot be trimmed to
              // its capitals, so it is never on the line: the label is an element.
              if (!skip && items.length >= 2 && items.some(i => i.kind === 'bare'))
                bare.push(path(el) + ' — ' + items.map(i => i.kind === 'bare' ? 'text ' + i.label : i.label).join(', '));
            }

            // A painted box that holds one line of text and nothing else; a box with an icon or
            // a label in it is a row, and measured as one.
            // Inline, a box is drawn around its font's ascent and descent on purpose: it is a
            // tint on running text, and running text has descenders. <code> is that tint
            // wherever it stands.
            if (painted(cs) && cs.display !== 'inline' && el.tagName !== 'CODE' && !isIcon(el) && el.textContent.trim()
                && el.children.length === 0
                && !['INPUT', 'SELECT', 'TEXTAREA', 'IMG', 'svg'].includes(el.tagName)) {
              const r = document.createRange(); r.selectNodeContents(el);
              const rects = [...r.getClientRects()].filter(x => x.width > 0 && x.height > 0);
              const b = el.getBoundingClientRect();
              const mid = rects.length ? (Math.min(...rects.map(x => x.top)) + Math.max(...rects.map(x => x.bottom))) / 2 : 0;
              // Only a box whose layout centres its line: one whose text sits at the top of a
              // taller box is not claiming to be centred.
              if (rects.length && rects.every(x => Math.abs(x.top + x.height / 2 - (rects[0].top + rects[0].height / 2)) < 2)
                  && Math.abs(mid - (b.top + b.height / 2)) < 1.5) {
                const bt = parseFloat(cs.borderTopWidth), bb = parseFloat(cs.borderBottomWidth);
                const bl = parseFloat(cs.borderLeftWidth), br = parseFloat(cs.borderRightWidth);
                const inner = { left: b.left + bl, top: b.top + bt, width: b.width - bl - br, height: b.height - bt - bb };
                // Measured across the text's own width only, so nothing else the box draws — an
                // indicator, a stripe — is taken for it.
                const left = Math.max(inner.left, Math.min(...rects.map(x => x.left)));
                const right = Math.min(inner.left + inner.width, Math.max(...rects.map(x => x.right)));
                if (inner.height >= 4 && right - left >= 2)
                {
                  const it = box({ left, top: inner.top, width: right - left, height: inner.height }, 'box', path(el), el);
                  it.line = [Math.min(...rects.map(x => x.top)) + sy, Math.max(...rects.map(x => x.bottom)) + sy];
                  boxes.push(it);
                }
              }
            }
          }
          // Everything is hit-testable while the page is measured: a drawing that ignores the
          // pointer — a decoration over the page — covers what is under it all the same.
          const hittable = document.createElement('style');
          hittable.setAttribute('data-align-scan', '');
          hittable.textContent = '*, *::before, *::after { pointer-events: auto !important; }';
          document.head.appendChild(hittable);
          // Every measured element by number, with where it stood, for each band to read again.
          const all = [...rows.flatMap(r => r.items), ...boxes];
          all.forEach((it, k) => { it.k = k; });
          window.__alignNodes = all.map(it => it.node);
          window.__alignAt = all.map(it => it.node.getBoundingClientRect().top + scrollY);
          // Pinned — sticky or fixed, or inside something that is: placed by the viewport, so
          // it moves with the scroll, and WebKit moves one pinned to the viewport's bottom while
          // it takes a picture.
          window.__alignPinned = all.map(it => {
            for (let e = it.node; e && e !== document.body; e = e.parentElement) {
              const p = getComputedStyle(e).position;
              if (p === 'sticky' || p === 'fixed') return 1;
            }
            return 0;
          });
          const height = document.documentElement.scrollHeight;
          return JSON.stringify({ rows, boxes, bare, height });
        }
        """;
}
