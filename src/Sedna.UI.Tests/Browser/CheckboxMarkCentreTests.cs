using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The mark inside every drawn checkbox — a tick, or the dash of a half-ticked one — is centred in
/// its box, measured on the paint, wherever the box lands.
/// </summary>
/// <remarks>
/// <para>
/// The tick was once a rotated border. A transformed element is not pixel-snapped in Chromium while the
/// box under it is, so the tick sat up to a pixel higher or lower in the same box depending only on the
/// fraction of a pixel the box's top fell on — "lower" in one example and "centred" in the next. It is a
/// mask now, painted into the snapped box. Every context is therefore measured at four fractional
/// offsets, at one and at two device pixels per CSS pixel: a screenshot taken at <see cref="Ink.Scale"/>
/// would hide a shift of a CSS pixel behind its own finer grid.
/// </para>
/// </remarks>
public class CheckboxMarkCentreTests : ScriptTestBase
{
    /// <summary>How far the ink's bounding box may sit from its box's centre, in CSS pixels.</summary>
    private const double Tolerance = 0.5;

    private static readonly string[] Offsets = ["0px", "0.25px", "0.5px", "0.75px"];

    private const string Contexts = """
        <style>*, *::before, *::after { transition: none !important; } .at { padding-top: var(--at); }</style>
        <div class="at" id="plain"><label class="form-check"><input type="checkbox" checked /> <span>Plain</span></label></div>
        <div class="at"><div class="form-field"><label class="form-label" for="r">Name</label>
          <label class="form-check"><input type="checkbox" id="in-field" checked /> <span>In a field</span></label></div></div>
        <div class="at"><fieldset class="form-section form-section--box"><legend><label class="form-check"><input type="checkbox" id="in-legend" checked /> <span>In a legend</span></label></legend>
          <div class="form-section-body"><label class="form-check"><input type="checkbox" id="in-body" checked /> <span>In its body</span></label></div></fieldset></div>
        <div class="at"><fieldset class="form-section form-section--box"><legend><label class="switch"><input type="checkbox" /> <span>Off</span></label></legend>
          <div class="form-section-body choice-dependent choice-dependent--dim"><label class="form-check"><input type="checkbox" id="in-dim" checked /> <span>In a dimmed body</span></label></div></fieldset></div>
        <div class="at"><details class="form-section form-section--box" open><summary><span class="form-section-legend">Folding</span></summary>
          <div class="form-section-body"><label class="form-check"><input type="checkbox" id="in-details" checked /> <span>In a folding box</span></label></div></details></div>
        <div class="at"><table class="table"><tbody><tr><td><label class="form-check"><input type="checkbox" id="in-cell" checked /> <span>In a cell</span></label></td></tr></tbody></table></div>
        <div class="at"><div class="form-check-list"><label class="form-check"><input type="checkbox" id="in-list" checked /> <span>In a list</span></label>
          <label class="form-check"><input type="checkbox" id="disabled" checked disabled /> <span>Disabled</span></label></div></div>
        <div class="at"><div class="form-check-list form-check-list--sm"><label class="form-check"><input type="checkbox" id="sm" checked /> <span>Small</span></label></div></div>
        <div class="at"><div class="form-check-list form-check-list--lg"><label class="form-check"><input type="checkbox" id="lg" checked /> <span>Large</span></label></div></div>
        <div class="at"><label class="form-check" style="font-size: 11px"><input type="checkbox" id="font-11" checked /> <span>Eleven</span></label></div>
        <div class="at"><label class="form-check" style="font-size: 23px; line-height: 1.7"><input type="checkbox" id="font-23" checked /> <span>Twenty-three</span></label></div>
        <div class="at"><label class="form-check"><input type="checkbox" id="prop" /> <span>Half</span></label></div>
        <div class="at"><label class="form-check"><input type="checkbox" id="aria" aria-checked="mixed" /> <span>Mixed</span></label></div>
        <div class="at"><ul class="tree" role="tree" aria-label="Locations" aria-multiselectable="true">
          <li role="treeitem" aria-checked="true"><span class="tree-row"><span class="tree-check" id="tree"></span><span class="tree-label">North hub</span></span></li>
          <li role="treeitem" aria-checked="mixed"><span class="tree-row"><span class="tree-check" id="tree-mixed"></span><span class="tree-label">South hub</span></span></li></ul></div>
        """;

    public static TheoryData<string, int> Cases()
    {
        var data = new TheoryData<string, int>();
        foreach (var variant in new[] { "dark", "light" })
            foreach (var scale in new[] { 1, 2 })
                data.Add(variant, scale);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_mark_is_centred_in_its_box_at_every_fractional_position(string variant, int scale)
    {
        if (NoBrowser) return;
        var failures = new List<string>();
        foreach (var offset in Offsets)
        {
            var (page, errors) = await OpenStyled(Contexts, deviceScaleFactor: scale);
            await Js.Run(page, """
                ([variant, offset]) => {
                    document.documentElement.dataset.variant = variant;
                    document.documentElement.style.setProperty('--at', offset);
                    document.getElementById('prop').indeterminate = true;
                }
                """, new[] { variant, offset });
            await Js.Settle(page);

            // The checkbox itself, or the drawn box of a tree row.
            foreach (var id in new[] { "plain", "in-field", "in-legend", "in-body", "in-dim", "in-details", "in-cell", "in-list", "disabled",
                                       "sm", "lg", "font-11", "font-23", "prop", "aria", "tree", "tree-mixed" })
            {
                var locator = id == "plain" ? page.Locator("#plain input") : page.Locator("#" + id);
                var (dx, dy) = await Offset(page, locator, scale);
                var label = $"{id} @ {offset}, {variant}, {scale}x";
                if (Math.Abs(dx) > Tolerance) failures.Add($"{label}: the mark is {dx:0.00}px off the box's centre horizontally");
                if (Math.Abs(dy) > Tolerance) failures.Add($"{label}: the mark is {dy:0.00}px off the box's centre vertically");
            }
            Assert.Empty(errors);
            await page.Context.DisposeAsync();
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public async Task The_mark_sits_where_it_does_whatever_fraction_the_box_falls_on()
    {
        // The defect itself: in Chromium the same box at 0.25px and at 0.5px put the tick a whole
        // pixel apart. The same box, so the spread of its offsets must be a fraction of a pixel.
        if (NoBrowser) return;
        var dys = new List<double>();
        foreach (var offset in Offsets)
        {
            var (page, _) = await OpenStyled(Contexts, deviceScaleFactor: 1);
            await Js.Run(page, "(offset) => document.documentElement.style.setProperty('--at', offset)", offset);
            await Js.Settle(page);
            dys.Add((await Offset(page, page.Locator("#plain input"), 1)).Dy);
            await page.Context.DisposeAsync();
        }
        Assert.True(dys.Max() - dys.Min() <= 0.5, $"The tick moved {dys.Max() - dys.Min():0.00}px between fractional positions: {string.Join(", ", dys.Select(d => d.ToString("0.00")))}.");
    }

    private const string MenuContexts = """
        <style>*, *::before, *::after { transition: none !important; } .at { padding-top: var(--at); }</style>
        <div class="at"><div class="menu" style="position: static; display: block; width: 260px">
          <button class="menu-item" type="button">Reader</button>
          <button class="menu-item" type="button" aria-current="true" id="current">Maintainer</button>
          <button class="menu-item" type="button">Owner <span class="menu-item-note">⌘O</span></button></div></div>
        """;

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_tick_of_a_menus_current_item_is_centred_in_its_box_at_every_fractional_position(string variant, int scale)
    {
        // A menu's current item carries the same tick, on the item's tint instead of a fill of its own,
        // so its box is the one the stylesheet places — read from the pseudo-element's used geometry.
        if (NoBrowser) return;
        var failures = new List<string>();
        foreach (var offset in Offsets)
        {
            var (page, errors) = await OpenStyled(MenuContexts, deviceScaleFactor: scale);
            await Js.Run(page, """
                ([variant, offset]) => {
                    document.documentElement.dataset.variant = variant;
                    document.documentElement.style.setProperty('--at', offset);
                }
                """, new[] { variant, offset });
            await Js.Settle(page);

            var box = await page.EvaluateAsync<double[]>("""
                () => {
                    const item = document.getElementById('current');
                    const r = item.getBoundingClientRect();
                    const c = getComputedStyle(item, '::after');
                    const w = parseFloat(c.width), h = parseFloat(c.height);
                    return [r.right - parseFloat(c.right) - w, r.top + parseFloat(c.top), w, h];
                }
                """);
            // A whole-pixel clip around the box, so the page and the image share a grid; the tick must
            // sit on the middle of the box the stylesheet placed, to within the device pixel it snaps to.
            const int pad = 3;
            var cx = Math.Floor(box[0]) - pad;
            var cy = Math.Floor(box[1]) - pad;
            var png = await page.ScreenshotAsync(new() { Clip = new() { X = (float)cx, Y = (float)cy, Width = (float)(Math.Ceiling(box[2]) + 2 * pad + 1), Height = (float)(Math.Ceiling(box[3]) + 2 * pad + 1) } });
            var image = Ink.Png.Decode(png);
            var ground = image.Pixel(0, 0);
            // Half the strongest contrast in the clip is where the ink's edge is, whatever the theme's
            // contrast: a fixed threshold cuts the thin end of a stroke short on a pale ground.
            var peak = 0;
            for (var y = 0; y < image.Height; y++)
                for (var x = 0; x < image.Width; x++)
                {
                    var c = image.Pixel(x, y);
                    peak = Math.Max(peak, Math.Abs(c.R - ground.R) + Math.Abs(c.G - ground.G) + Math.Abs(c.B - ground.B));
                }
            int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
            for (var y = 0; y < image.Height; y++)
                for (var x = 0; x < image.Width; x++)
                {
                    var c = image.Pixel(x, y);
                    if (Math.Abs(c.R - ground.R) + Math.Abs(c.G - ground.G) + Math.Abs(c.B - ground.B) > peak / 2)
                    {
                        x0 = Math.Min(x0, x); x1 = Math.Max(x1, x + 1);
                        y0 = Math.Min(y0, y); y1 = Math.Max(y1, y + 1);
                    }
                }
            Assert.True(x1 > x0, "The current item paints no tick.");
            var dx = ((x0 + x1) / 2.0 - (box[0] + box[2] / 2 - cx) * scale) / scale;
            var dy = ((y0 + y1) / 2.0 - (box[1] + box[3] / 2 - cy) * scale) / scale;
            var label = $"menu @ {offset}, {variant}, {scale}x";
            if (Math.Abs(dx) > Tolerance) failures.Add($"{label}: the tick is {dx:0.00}px off its box's centre horizontally");
            if (Math.Abs(dy) > Tolerance) failures.Add($"{label}: the tick is {dy:0.00}px off its box's centre vertically");
            Assert.Empty(errors);
            await page.Context.DisposeAsync();
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// How far the middle of what is painted over a box's fill sits from the middle of the box,
    /// in CSS pixels, read from a screenshot at the page's own device scale.
    /// </summary>
    private static async Task<(double Dx, double Dy)> Offset(IPage page, ILocator box, int scale)
    {
        await box.ScrollIntoViewIfNeededAsync();
        var r = (await box.BoundingBoxAsync()) ?? throw new InvalidOperationException("The box has no rectangle.");
        const int pad = 3;
        var png = await page.ScreenshotAsync(new() { Clip = new() { X = r.X - pad, Y = r.Y - pad, Width = r.Width + 2 * pad, Height = r.Height + 2 * pad } });
        var image = Ink.Png.Decode(png);
        static int Diff((int R, int G, int B) a, (int R, int G, int B) b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

        // The box is the connected region of its own fill — the border is the same colour once ticked —
        // found from a point just inside its top edge. A neighbour in the clip (the next row of a list)
        // is therefore never taken for part of it, and the box is the one that was snapped, not a guess.
        var sx = Math.Min(image.Width - 1, (int)Math.Round((pad + r.Width / 2) * scale));
        var sy = Math.Min(image.Height - 1, (int)Math.Round(pad * scale + 1.5 * scale));
        var fill = image.Pixel(sx, sy);
        var seen = new bool[image.Width, image.Height];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((sx, sy));
        seen[sx, sy] = true;
        int bx0 = int.MaxValue, bx1 = -1, by0 = int.MaxValue, by1 = -1;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            bx0 = Math.Min(bx0, x); bx1 = Math.Max(bx1, x);
            by0 = Math.Min(by0, y); by1 = Math.Max(by1, y);
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (nx >= 0 && ny >= 0 && nx < image.Width && ny < image.Height && !seen[nx, ny] && Diff(image.Pixel(nx, ny), fill) <= 40)
                {
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
        }
        Assert.True(bx1 > bx0, "The box paints nothing.");

        var inset = 2 * scale;
        int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
        for (var y = by0 + inset; y <= by1 - inset; y++)
            for (var x = bx0 + inset; x <= bx1 - inset; x++)
                if (Diff(image.Pixel(x, y), fill) > 150)
                {
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x + 1);
                    y0 = Math.Min(y0, y); y1 = Math.Max(y1, y + 1);
                }
        Assert.True(x1 > x0, "Nothing is painted over the box's fill — no mark.");

        var boxX = (bx0 + bx1 + 1) / 2.0;
        var boxY = (by0 + by1 + 1) / 2.0;
        return (((x0 + x1) / 2.0 - boxX) / scale, ((y0 + y1) / 2.0 - boxY) / scale);
    }
}
