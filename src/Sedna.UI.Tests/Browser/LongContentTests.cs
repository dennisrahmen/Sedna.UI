using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A long unbroken string — a file name, an address, an id — never pushes a component
/// wider than the box it is in.
/// </summary>
/// <remarks>
/// <para>
/// Each case is a component's text role holding a token far longer than its 280px box. A
/// flex row whose text will not shrink below its longest word was the common cause: an
/// alert, a toast, a card's title, a key/value row, a menu item each ran out of their
/// container, and an app could not fix the toast, which the library draws.
/// </para>
/// <para>
/// A component whose text is meant to stay on one line (a badge, a chip's label) truncates
/// instead and is held to the same edge. A new component with a text role belongs here.
/// </para>
/// </remarks>
public class LongContentTests : ScriptTestBase
{
    private const string Token = "orders-export-2026-11-06T09-41-52Z-north-west-region-final-final-v3.csv";

    public static TheoryData<string, string> Roles() => new()
    {
        { "alert", $"""<div class="alert alert--info"><i class="ri-information-line"></i><span>Saved as {Token}</span></div>""" },
        { "alert, bare text", $"""<div class="alert alert--info">Saved as {Token}</div>""" },
        { "card title", $"""<div class="card"><div class="card-head"><h3>{Token}</h3><button class="btn btn--sm" type="button">Open</button></div></div>""" },
        { "card body", $"""<div class="card"><div class="card-body"><p>{Token}</p></div></div>""" },
        { "key/value", $"""<div class="kv"><span class="k">File</span><span class="v">{Token}</span></div>""" },
        { "menu item", $"""<div class="menu" style="position:static"><button class="menu-item" type="button">{Token}</button></div>""" },
        { "list row", $"""<ul class="list"><li class="list-row">{Token}</li></ul>""" },
        { "chip", $"""<span class="chip"><span class="chip-label">{Token}</span></span>""" },
        { "form hint", $"""<div class="form-field"><label class="form-label">File</label><input class="form-input"><p class="form-hint">{Token}</p></div>""" },
        { "page head", $"""<div class="page-head"><h1>{Token}</h1></div>""" },
        { "callout", $"""<div class="callout callout--info"><i class="ri-information-line"></i><div class="callout-body"><span class="callout-title">{Token}</span>{Token}</div></div>""" },
    };

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task A_long_token_stays_inside_its_box(string role, string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div id="box" style="width:280px">{markup}</div>""");

        var spill = await page.EvaluateAsync<double>("""
            () => {
                const box = document.getElementById('box').getBoundingClientRect();
                let worst = 0;
                for (const el of document.querySelectorAll('#box *')) {
                    const r = el.getBoundingClientRect();
                    if (r.width === 0) continue;
                    // A floating panel is sized by the viewport, not by the box it hangs
                    // from: what is inside it is held to the panel instead.
                    if (el.matches('.menu')) continue;
                    const panel = el.closest('.menu');
                    const limit = panel ? panel.getBoundingClientRect() : box;
                    worst = Math.max(worst, r.right - limit.right, limit.left - r.left);
                }
                return worst;
            }
            """);

        Assert.True(spill <= 1, $"The {role} runs {spill:0}px out of its 280px box.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_toast_holds_a_long_token()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("<p>x</p>");
        await page.SetViewportSizeAsync(360, 640);
        await Js.Run(page, $"() => sednaUi.toast('Saved as {Token}', {{ timeout: 0 }})");
        await page.WaitForFunctionAsync("() => { const t = document.querySelector('.toast'); return t && !t.hidden; }");

        var overflow = await page.EvaluateAsync<double>(
            "() => { const b = document.querySelector('.toast-body'); return b.scrollWidth - b.clientWidth; }");
        Assert.True(overflow <= 1, $"The toast's text runs {overflow:0}px out of it.");
    }

    [Fact]
    public async Task A_code_token_in_a_squeezed_table_keeps_its_word_and_the_table_scrolls()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="sedna-scroll-x" id="scroll" style="width:300px">
                <table class="table"><thead><tr><th>File</th><th>SHA-256</th></tr></thead>
                <tbody><tr><td>orders-export.csv</td><td><code id="sum">9f86d081884c7d659a2feaa0c55ad015</code></td></tr></tbody></table>
            </div>
            """);

        var lines = await page.EvaluateAsync<double>(
            "() => { const c = document.getElementById('sum'); return c.getBoundingClientRect().height / parseFloat(getComputedStyle(c).lineHeight); }");
        Assert.True(lines < 1.5, $"The code broke onto {lines:0.0} lines instead of the table scrolling.");
        Assert.True(await page.EvaluateAsync<bool>("() => { const s = document.getElementById('scroll'); return s.scrollWidth > s.clientWidth; }"));
    }

    [Fact]
    public async Task A_detail_row_keeps_wide_content_inside_the_table()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <table class="table" id="t" style="width:400px">
                <tbody>
                    <tr><td>Order 4209</td><td>Open</td></tr>
                    <tr class="tr-detail"><td colspan="2"><div class="tr-detail-body" id="d"><pre style="margin:0">a line far wider than the table it is in, which must scroll on its own rather than widen every column above it</pre></div></td></tr>
                </tbody>
            </table>
            """);

        Assert.Equal(400, await page.EvaluateAsync<double>("() => Math.round(document.getElementById('t').getBoundingClientRect().width)"));
        Assert.True(await page.EvaluateAsync<bool>("() => { const d = document.getElementById('d'); return d.scrollWidth > d.clientWidth; }"),
            "The wide content does not scroll inside the detail row.");
    }

    [Fact]
    public async Task A_filled_page_scrolls_instead_of_squeezing_a_clipping_child()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="page page--fill" id="p" style="height:300px; overflow:auto">
                <div class="card" id="c" style="overflow:hidden"><div style="height:800px">tall</div></div>
            </div>
            """);

        Assert.True(await page.EvaluateAsync<double>("() => document.getElementById('c').getBoundingClientRect().height") >= 800,
            "The card was squeezed to fit the page instead of the page scrolling.");
    }

    [Fact]
    public async Task A_button_in_a_narrow_alert_keeps_its_label_inside_it()
    {
        if (NoBrowser) return;
        // Every child of an alert may shrink so its text can wrap; a button that shrank with
        // them pushed its own label out of its border at a phone's width.
        var (page, _) = await OpenStyled("""
            <div style="width:310px">
                <div class="alert alert--warn">
                    <i class="ri-alert-line"></i>
                    <span>Your session expires in 2 minutes.</span>
                    <button class="btn btn--sm sedna-push" type="button" id="b">Stay signed in</button>
                </div>
            </div>
            """);

        Assert.True(await page.EvaluateAsync<bool>("() => { const b = document.getElementById('b'); return b.scrollWidth <= b.clientWidth; }"),
            "The button's label spills out of the button.");
    }
}
