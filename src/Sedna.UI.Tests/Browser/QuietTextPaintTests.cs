using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A placeholder, the dash of an unavailable stat and a primary button's edge, as Chromium paints
/// them: the cascade, not the token table, decides what reaches the screen.
/// </summary>
/// <remarks>
/// <c>PlaceholderContrastTests</c> and <c>ButtonEdgeContrastTests</c> measure every palette and
/// theme from the token blocks. This measures the rendered page in both variants, so a later rule
/// in another part — a layer, a modifier, a beam — that repaints one of them is caught too.
/// </remarks>
public class QuietTextPaintTests : ScriptTestBase
{
    private const string Body = """
        <div class="card" style="width:360px">
          <div class="card-body">
            <input class="form-input" id="field" placeholder="Search orders">
            <div class="stat stat--unavailable"><span class="stat-label">Queue depth</span><span class="stat-value" id="dash">—</span></div>
            <button class="btn btn--brand btn--solid" type="button" id="primary">Save</button>
          </div>
        </div>
        """;

    private sealed class Ratios
    {
        public double Placeholder { get; set; }
        public double Dash { get; set; }
        public double Edge { get; set; }
        public double Fill { get; set; }
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Placeholder_and_unavailable_stat_read_at_AA_and_the_primary_edge_at_3_to_1(string variant)
    {
        if (NoBrowser) return;

        // Stamped in <head>, before the body is styled: flipping it afterwards would start the
        // button's and the field's colour transitions, and read them halfway.
        var (page, errors) = await OpenStyled(Body,
            $"<script>document.documentElement.setAttribute('data-variant', '{variant}')</script>");

        var ratios = await page.EvaluateAsync<Ratios>("""
            () => {
                const rgb = c => (c.match(/[\d.]+/g) || []).map(Number).slice(0, 3);
                const lum = ([r, g, b]) => [r, g, b].map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; })
                    .reduce((s, v, i) => s + v * [0.2126, 0.7152, 0.0722][i], 0);
                const ratio = (a, b) => { const [x, y] = [lum(rgb(a)), lum(rgb(b))].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
                const field = document.getElementById('field');
                const card = getComputedStyle(document.querySelector('.card')).backgroundColor;
                const button = getComputedStyle(document.getElementById('primary'));
                return {
                    placeholder: ratio(getComputedStyle(field, '::placeholder').color, getComputedStyle(field).backgroundColor),
                    dash: ratio(getComputedStyle(document.getElementById('dash')).color, card),
                    edge: ratio(button.borderTopColor, card),
                    fill: ratio(button.backgroundColor, card),
                };
            }
            """);

        Assert.Empty(errors);
        Assert.True(ratios.Placeholder >= 4.5, $"{variant}: the placeholder reads at {ratios.Placeholder:0.00}:1.");
        Assert.True(ratios.Dash >= 4.5, $"{variant}: an unavailable stat's dash reads at {ratios.Dash:0.00}:1.");
        Assert.True(Math.Max(ratios.Edge, ratios.Fill) >= 3,
            $"{variant}: the primary button's boundary is {ratios.Edge:0.00}:1 (edge), {ratios.Fill:0.00}:1 (fill).");
    }
}
