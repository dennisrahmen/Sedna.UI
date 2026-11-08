using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The pages of a multi-page form measured in a layout engine: the confirmation panel at the
/// end and the question page's legend.
/// </summary>
public class FormFlowLayoutTests : ScriptTestBase
{
    private static string Panel(string width, string reference) =>
        $"""
        <div style="width:{width}" id="box">
          <div class="confirmation-panel" id="panel">
            <h1 class="confirmation-panel-title" id="title">Application sent</h1>
            <p id="lead">Your reference number</p>
            <p class="confirmation-panel-ref" id="ref">
              <span id="code">{reference}</span>
              <button class="btn-bare" type="button" id="copy" data-copy="{reference}" aria-label="Copy the reference number"><i class="ri-file-copy-line"></i></button>
            </p>
          </div>
          <p id="go" style="color: var(--go-solid)">x</p>
          <p id="on" style="color: var(--on-solid)">x</p>
        </div>
        """;

    private sealed record PanelGeometry(
        double[] Panel, double[] Title, double[] Code, double[] Copy, string[] Paint, string PrintAdjust, double ScrollWidth, double ClientWidth);

    private const string MeasurePanel =
        """
        () => {
          const el = id => document.getElementById(id);
          const r = id => { const b = el(id).getBoundingClientRect(); return [b.left, b.right, b.top, b.bottom]; };
          const text = id => { const g = document.createRange(); g.selectNodeContents(el(id)); const b = g.getBoundingClientRect(); return [b.left, b.right, b.top, b.bottom]; };
          const cs = getComputedStyle(el('panel'));
          return {
            panel: r('panel'), title: text('title'), code: r('code'), copy: r('copy'),
            paint: [cs.backgroundColor, cs.borderTopColor, cs.color, getComputedStyle(el('go')).color, getComputedStyle(el('on')).color,
                    getComputedStyle(el('ref')).fontFamily, getComputedStyle(document.body).fontFamily],
            printAdjust: cs.printColorAdjust || cs.webkitPrintColorAdjust,
            scrollWidth: el('panel').scrollWidth, clientWidth: el('panel').clientWidth,
          };
        }
        """;

    [Fact]
    public async Task The_confirmation_panel_centres_its_heading_and_reference_on_the_go_fill()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Panel("640px", "TRD-7Q4K2M"));
        var g = await Read<PanelGeometry>(page, MeasurePanel);

        var centre = (g.Panel[0] + g.Panel[1]) / 2;
        Assert.InRange((g.Title[0] + g.Title[1]) / 2, centre - 1, centre + 1);
        // The reference and its copy button are centred together, on one line.
        Assert.InRange((g.Code[0] + g.Copy[1]) / 2, centre - 1, centre + 1);
        Assert.True(g.Copy[0] > g.Code[1], "The copy button is not beside the reference.");
        Assert.True(g.Copy[2] < g.Code[3] && g.Copy[3] > g.Code[2], "The copy button left the reference's line.");
        // The fill is the go colour, the words are on-solid, and the border is the fill's own
        // colour, so it shows only where the fill is dropped.
        Assert.Equal(g.Paint[3], g.Paint[0]);
        Assert.Equal(g.Paint[0], g.Paint[1]);
        Assert.Equal(g.Paint[4], g.Paint[2]);
        // The reference is set in the mono face, not the body's.
        Assert.NotEqual(g.Paint[6], g.Paint[5]);
        // White words on a fill: the fill prints with them.
        Assert.Equal("exact", g.PrintAdjust);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_long_reference_wraps_inside_a_narrow_panel()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Panel("280px", "TRD-7Q4K2M-2026-ORDERS-ACCOUNT-OPENING-0001"));
        var g = await Read<PanelGeometry>(page, MeasurePanel);

        Assert.True(g.ScrollWidth <= g.ClientWidth, $"The panel overflows: {g.ScrollWidth} in {g.ClientWidth}.");
        Assert.True(g.Code[1] <= g.Panel[1] && g.Code[0] >= g.Panel[0], "The reference runs out of the panel.");
        Assert.True(g.Copy[1] <= g.Panel[1] && g.Copy[0] >= g.Panel[0], "The copy button runs out of the panel.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_question_legend_is_the_page_heading_without_a_rule()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width:640px">
              <fieldset class="form-section form-section--question">
                <legend id="legend"><span class="eyebrow" id="eyebrow">Step 3 of 5</span><h1 id="q">Where should we deliver your orders?</h1></legend>
                <div class="form-section-body"><label class="form-check"><input type="radio" name="q" /> <span>Here</span></label></div>
              </fieldset>
              <h1 id="plain">Plain</h1>
            </div>
            """);

        var m = await page.EvaluateAsync<string[]>("""
            () => {
              const cs = id => getComputedStyle(document.getElementById(id));
              return [cs('legend').borderBottomWidth, cs('q').marginTop, cs('q').marginBottom, cs('q').fontSize, cs('plain').fontSize,
                      String(document.getElementById('q').getBoundingClientRect().top - document.getElementById('eyebrow').getBoundingClientRect().bottom)];
            }
            """);

        Assert.Equal("0px", m[0]);
        Assert.Equal("0px", m[1]);
        Assert.Equal("0px", m[2]);
        // The question keeps the size of every other page heading.
        Assert.Equal(m[4], m[3]);
        // The eyebrow sits just above the question it names.
        Assert.InRange(double.Parse(m[5], System.Globalization.CultureInfo.InvariantCulture), 0, 8);
        Assert.Empty(errors);
    }

    private static async Task<T> Read<T>(Microsoft.Playwright.IPage page, string script) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(
            await page.EvaluateAsync<string>("() => JSON.stringify((" + script + ")())"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
}
