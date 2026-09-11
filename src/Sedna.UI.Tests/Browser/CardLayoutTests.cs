using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A card's parts, measured in a layout engine: the gutter they share, and how compact
/// density moves it.
/// </summary>
/// <remarks>
/// Compact density used to reach `.table` and nothing else, so an app whose dense view
/// is a list of cards wrote its own <c>:root[data-density="compact"] .card-body</c> rule
/// and fought the library's specificity for it. The card gutter is now
/// <c>--card-pad-block</c> / <c>--card-pad-inline</c>, which the density block remaps.
/// </remarks>
public class CardLayoutTests : ScriptTestBase
{
    private const string Fixture =
        """
        <div style="width:560px">
          <div class="card">
            <div class="card-head" id="head">
              <strong>Title</strong>
              <div class="card-head-right"><span>Right</span></div>
            </div>
            <div class="card-body" id="body">Body text</div>
            <div class="card-foot" id="foot"><button class="btn">Save</button></div>
            <div class="card-warning" id="warning">A stale figure</div>
          </div>

          <div class="card">
            <div class="card-head card-head--tabs">
              <div class="tabs" role="tablist">
                <button class="tab" role="tab" aria-selected="true" id="tab">Open</button>
              </div>
              <div class="card-head-right" id="tabs-right"><span>3</span></div>
            </div>
            <div class="card-body">Panel</div>
          </div>

          <div class="card">
            <div class="sticky-group-head" id="group">Today</div>
            <ul class="list"><li><div class="list-row" id="row">A row</div></li></ul>
          </div>
        </div>
        """;

    /// <summary>
    /// Every padding that has to line up with a card's text, with the value it has in
    /// comfortable density and the value it has in compact.
    /// </summary>
    /// <remarks>
    /// The comfortable column is not derived from the tokens. It is the literal each rule
    /// carried before the tokens existed — `--space-6` is 12px, `--space-7` 16px and
    /// `--space-4` 8px — so a token whose default drifts fails here even though every rule
    /// still agrees with every other.
    /// </remarks>
    private static readonly (string Id, string Property, string Comfortable, string Compact)[] Gutter =
    [
        ("head",       "paddingTop",         "12px", "8px"),
        ("head",       "paddingBottom",      "12px", "8px"),
        ("head",       "paddingInlineStart", "16px", "12px"),
        ("head",       "paddingInlineEnd",   "16px", "12px"),
        ("body",       "paddingTop",         "12px", "8px"),
        ("body",       "paddingBottom",      "12px", "8px"),
        ("body",       "paddingInlineStart", "16px", "12px"),
        ("body",       "paddingInlineEnd",   "16px", "12px"),
        ("foot",       "paddingTop",         "12px", "8px"),
        ("foot",       "paddingBottom",      "12px", "8px"),
        ("foot",       "paddingInlineStart", "16px", "12px"),
        ("foot",       "paddingInlineEnd",   "16px", "12px"),
        // The warning strip keeps its own tighter block padding; only its gutter follows.
        ("warning",    "paddingTop",         "8px",  "8px"),
        ("warning",    "paddingInlineStart", "16px", "12px"),
        // The first tab's label sits on the body's gutter, in both densities.
        ("tab",        "paddingTop",         "8px",  "8px"),
        ("tab",        "paddingInlineStart", "16px", "12px"),
        ("tabs-right", "paddingInlineEnd",   "16px", "12px"),
        // Rows and group heads that meet the card's edges keep their text on its gutter.
        ("row",        "paddingInlineStart", "16px", "12px"),
        ("group",      "paddingInlineStart", "16px", "12px"),
    ];

    private static async Task<string[]> Measure(IPage page) =>
        await page.EvaluateAsync<string[]>(
            "pairs => pairs.map(([id, prop]) => getComputedStyle(document.getElementById(id))[prop])",
            Gutter.Select(g => new[] { g.Id, g.Property }).ToArray());

    private static List<string> Mismatches(string[] measured, Func<(string Id, string Property, string Comfortable, string Compact), string> expected) =>
        Gutter.Select((g, i) => (g, i))
            .Where(x => measured[x.i] != expected(x.g))
            .Select(x => $"#{x.g.Id} {x.g.Property}: expected {expected(x.g)}, measured {measured[x.i]}")
            .ToList();

    [Fact]
    public async Task Moving_the_card_padding_onto_tokens_changed_no_default_value()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var off = Mismatches(await Measure(page), g => g.Comfortable);

        Assert.True(off.Count == 0,
            "A card part's comfortable padding moved. The tokens were introduced to carry the "
            + "existing values, not to change them:" + Environment.NewLine
            + string.Join(Environment.NewLine, off));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Compact_density_tightens_every_card_part_together()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.EvaluateAsync("() => document.documentElement.setAttribute('data-density', 'compact')");
        var off = Mismatches(await Measure(page), g => g.Compact);

        // One number each, not a per-part guess: a part that did not move is a card whose
        // text no longer lines up with the head above it.
        Assert.True(off.Count == 0,
            "Compact density left a card part at its comfortable padding, or moved it to a value "
            + "the other parts do not share:" + Environment.NewLine
            + string.Join(Environment.NewLine, off));
        Assert.Empty(errors);
    }

    private const string PartsFixture =
        """
        <div style="width:560px">
          <div class="card">
            <div class="card-head"><strong>Reservation</strong></div>
            <div class="card-body">
              <div class="card-section" id="first-section">Details</div>
              <div class="card-section" id="second-section">Attachments</div>
              <div class="card-section" id="third-section">History</div>
            </div>
          </div>

          <div class="card">
            <div class="card-body">Body</div>
            <div class="card-foot" id="plain-foot">
              <button class="btn" id="plain-first">Cancel</button>
              <button class="btn btn-primary">Save</button>
            </div>
          </div>

          <div class="card">
            <div class="card-body">Body</div>
            <div class="card-foot card-foot--start" id="start-foot">
              <button class="btn" id="start-first">Approve</button>
              <button class="btn">Send back</button>
              <button class="btn sedna-push" id="start-trailing">More</button>
            </div>
          </div>
        </div>
        """;

    [Fact]
    public async Task A_card_section_is_divided_from_whatever_precedes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(PartsFixture);

        var report = await page.EvaluateAsync<string[]>(@"() => {
            const out = [];
            const cs = id => getComputedStyle(document.getElementById(id));
            const want = (what, got, expected) => {
                if (got !== expected) out.push(what + ': expected ' + expected + ', measured ' + got);
            };

            // The first section draws no rule: the .card-head above it already draws one,
            // and a section that is not rendered must not leave a stray line behind.
            const first = cs('first-section');
            want('first borderTopWidth', first.borderTopWidth, '0px');
            want('first marginTop', first.marginTop, '0px');
            want('first paddingTop', first.paddingTop, '0px');

            for (const id of ['second-section', 'third-section']) {
                const s = cs(id);
                want(id + ' borderTopWidth', s.borderTopWidth, '1px');
                want(id + ' borderTopStyle', s.borderTopStyle, 'solid');
                // The card's own block gutter either side of the rule.
                want(id + ' marginTop', s.marginTop, '12px');
                want(id + ' paddingTop', s.paddingTop, '12px');
            }

            document.documentElement.setAttribute('data-density', 'compact');
            const compact = cs('second-section');
            want('compact marginTop', compact.marginTop, '8px');
            want('compact paddingTop', compact.paddingTop, '8px');
            document.documentElement.removeAttribute('data-density');
            return out;
        }");

        Assert.True(report.Length == 0,
            "A .card-section divides sub-sections of one body with a hairline and the card's own "
            + "spacing, and tightens with the rest of the card:" + Environment.NewLine
            + string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_leading_aligned_foot_moves_only_the_justification()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(PartsFixture);

        var report = await page.EvaluateAsync<string[]>(@"() => {
            const out = [];
            const box = id => document.getElementById(id).getBoundingClientRect();
            const cs = id => getComputedStyle(document.getElementById(id));

            const start = cs('start-foot'), plain = cs('plain-foot');
            for (const prop of ['paddingTop', 'paddingBottom', 'paddingLeft', 'paddingRight',
                                'borderTopWidth', 'gap', 'flexWrap', 'alignItems']) {
                if (start[prop] !== plain[prop]) {
                    out.push(prop + ': --start has ' + start[prop] + ', .card-foot has ' + plain[prop]);
                }
            }

            // The first action sits on the foot's leading edge...
            const foot = box('start-foot');
            const lead = foot.left + parseFloat(start.paddingLeft);
            if (Math.abs(box('start-first').left - lead) > 1) {
                out.push('the first action is ' + Math.round(box('start-first').left - lead)
                    + 'px from the leading edge');
            }

            // ...and .sedna-push still carries one control to the far end.
            const trail = foot.right - parseFloat(start.paddingRight);
            if (Math.abs(box('start-trailing').right - trail) > 1) {
                out.push('the .sedna-push control is ' + Math.round(trail - box('start-trailing').right)
                    + 'px short of the trailing edge');
            }

            // The plain foot is still right-aligned — this modifier is the departure.
            const plainLead = box('plain-foot').left + parseFloat(plain.paddingLeft);
            if (box('plain-first').left - plainLead < 20) {
                out.push('.card-foot is no longer right-aligned, so --start asserts nothing');
            }
            return out;
        }");

        Assert.True(report.Length == 0,
            ".card-foot--start changes where the row starts and nothing else about the foot:"
            + Environment.NewLine + string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }
}
