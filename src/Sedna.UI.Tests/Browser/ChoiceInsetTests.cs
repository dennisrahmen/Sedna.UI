using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What belongs to a choice control lines up with its label, measured in a layout
/// engine: the hint in <c>.choice-text</c>, and the block in <c>.choice-dependent</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>--choice-inset-check</c> and <c>--choice-inset-switch</c> restate two numbers the
/// control rules also hold — the box or track width and the gap after it. Nothing but a
/// measurement connects them, so this is the test that fails when one side changes
/// alone.
/// </para>
/// <para>
/// The request that produced these was an app indenting with a hand-measured 23px, which
/// is the checkbox's inset, under a switch, whose inset is not — and the catalogue's own
/// radio example had 24px. Both looked right to whoever wrote them.
/// </para>
/// </remarks>
public class ChoiceInsetTests : ScriptTestBase
{
    private const string Fixture =
        """
        <div style="padding:20px; width:520px; display:flex; flex-direction:column; gap:12px">
          <label class="form-check" id="check"><input type="checkbox" checked />
            <span class="choice-text"><span id="check-label">Notify</span><span class="choice-hint" id="check-hint">By e-mail.</span></span>
          </label>
          <div class="choice-dependent" id="check-dep">
            <div class="form-field">
              <label class="form-label">Recipients</label>
              <div class="input-group"><input class="form-input" value="alex.fischer@example.com" /><span class="input-affix">list</span></div>
            </div>
            <label class="switch" id="inner"><input type="checkbox" checked /><span id="inner-label">Close stale topics</span></label>
            <div class="choice-dependent" id="inner-dep"><input class="form-input" value="7" /></div>
          </div>

          <label class="form-check" id="radio"><input type="radio" name="r" checked /><span id="radio-label">Retry</span></label>
          <div class="choice-dependent" id="radio-dep"><input class="form-input" value="3" /></div>

          <label class="switch" id="switch"><input type="checkbox" checked />
            <span class="choice-text"><span id="switch-label">Housekeeping</span><span class="choice-hint" id="switch-hint">Nightly.</span></span>
          </label>
          <div class="choice-dependent" id="switch-dep"><input class="form-input" value="on" /></div>
        </div>
        """;

    private const string Inactive =
        """
        <div style="padding:20px; width:520px">
          <label class="form-check"><input type="checkbox" id="hide-box" /> Escalate</label>
          <div class="choice-dependent choice-dependent--hide" id="hide"><input class="form-input" /></div>

          <label class="switch"><input type="checkbox" id="dim-box" /> Forward</label>
          <div class="choice-dependent choice-dependent--dim" id="dim">
            <input class="form-input" id="dim-input" />
            <label class="form-check"><input type="checkbox" id="nested-box" /> Also copy</label>
            <div class="choice-dependent choice-dependent--dim" id="nested"><input class="form-input" /></div>
          </div>

          <label class="form-check"><input type="radio" name="r" id="radio-a" checked /> A</label>
          <label class="form-check"><input type="radio" name="r" id="radio-b" /> B</label>
          <div class="choice-dependent choice-dependent--hide" id="radio-dep"><input class="form-input" /></div>

          <label class="form-check"><input type="checkbox" checked /> Plain</label>
          <div class="choice-dependent" id="plain" hidden><input class="form-input" /></div>
        </div>
        """;

    /// <summary>The pairs whose start edges must agree: what follows, and the label it follows.</summary>
    private static readonly (string Follower, string Label)[] Pairs =
    [
        ("check-hint", "check-label"),
        ("check-dep", "check-label"),
        ("inner-dep", "inner-label"),
        ("radio-dep", "radio-label"),
        ("switch-hint", "switch-label"),
        ("switch-dep", "switch-label"),
    ];

    public static TheoryData<string, string> Layouts() => new()
    {
        { "ltr", "" },
        { "rtl", "" },
        { "ltr", "compact" },
        { "rtl", "compact" },
    };

    private async Task<(Microsoft.Playwright.IPage Page, List<string> Errors)> Prepare(
        string fixture, string dir, string density, string extraHead = "")
    {
        var (page, errors) = await OpenStyled(fixture, extraHead);
        await page.EvaluateAsync(
            "([dir, density]) => { document.documentElement.dir = dir;"
            + " if (density) document.documentElement.dataset.density = density; }",
            new[] { dir, density });
        return (page, errors);
    }

    /// <summary>The inline-start edge of each id, in its own writing direction.</summary>
    private static Task<double[]> Starts(Microsoft.Playwright.IPage page, string[] ids) =>
        page.EvaluateAsync<double[]>(
            "ids => ids.map(id => { const r = document.getElementById(id).getBoundingClientRect();"
            + " const rtl = getComputedStyle(document.documentElement).direction === 'rtl';"
            + " return Math.round((rtl ? -r.right : r.left) * 100) / 100; })",
            ids);

    [Theory]
    [MemberData(nameof(Layouts))]
    public async Task A_hint_and_a_dependent_block_start_where_the_label_starts(string dir, string density)
    {
        if (NoBrowser) return;
        var (page, errors) = await Prepare(Fixture, dir, density);

        var ids = Pairs.SelectMany(p => new[] { p.Follower, p.Label }).Distinct().ToArray();
        var starts = await Starts(page, ids);
        var at = ids.Zip(starts).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal);

        var off = Pairs.Where(p => at[p.Follower] != at[p.Label])
            .Select(p => $"{p.Follower} at {at[p.Follower]}, {p.Label} at {at[p.Label]}")
            .ToList();

        Assert.True(off.Count == 0,
            $"In {dir}{(density.Length > 0 ? $" at {density} density" : "")}, what follows a choice "
            + "control does not start where its label does: " + string.Join("; ", off));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_inset_follows_a_redefined_spacing_scale()
    {
        if (NoBrowser) return;
        var (page, errors) = await Prepare(
            Fixture, "ltr", "", "<style>:root { --space-4: 14px; --space-5: 18px; }</style>");

        var starts = await Starts(page, ["check-dep", "check-label", "switch-dep", "switch-label"]);

        Assert.Equal(starts[1], starts[0]);
        Assert.Equal(starts[3], starts[2]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_dependent_block_holds_an_input_group_without_overflowing()
    {
        if (NoBrowser) return;
        var (page, errors) = await Prepare(Fixture, "ltr", "");

        var overflow = await page.EvaluateAsync<bool>(
            "() => { const d = document.getElementById('check-dep');"
            + " const g = d.querySelector('.input-group').getBoundingClientRect();"
            + " return d.scrollWidth > d.clientWidth || g.right > d.getBoundingClientRect().right + 0.5; }");

        Assert.False(overflow, "An .input-group inside .choice-dependent overflows the indented block.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_inactive_modifiers_follow_the_control_and_nothing_else()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Inactive);

        const string State =
            """
            () => {
              const s = id => getComputedStyle(document.getElementById(id));
              const input = document.getElementById('dim-input');
              input.focus();
              const focusable = document.activeElement === input;
              input.blur();
              return {
                hide: s('hide').display,
                dimOpacity: s('dim').opacity,
                nestedOpacity: s('nested').opacity,
                focusable,
                radio: s('radio-dep').display,
                plain: s('plain').display,
              };
            }
            """;

        var off = await page.EvaluateAsync<InactiveState>(State);
        Assert.Equal("none", off.Hide);
        Assert.Equal("0.45", off.DimOpacity);
        Assert.Equal("1", off.NestedOpacity);
        Assert.False(off.Focusable, "A dimmed dependent block still takes focus.");
        Assert.Equal("none", off.Radio);
        Assert.Equal("none", off.Plain);

        await page.CheckAsync("#hide-box");
        await page.CheckAsync("#dim-box");
        await page.CheckAsync("#radio-b");

        var on = await page.EvaluateAsync<InactiveState>(State);
        Assert.Equal("flex", on.Hide);
        Assert.Equal("1", on.DimOpacity);
        Assert.Equal("0.45", on.NestedOpacity);
        Assert.True(on.Focusable, "A dependent block stays inert after its control is switched on.");
        Assert.Equal("flex", on.Radio);
        Assert.Equal("none", on.Plain);

        Assert.Empty(errors);
    }

    private sealed class InactiveState
    {
        public string Hide { get; set; } = "";
        public string DimOpacity { get; set; } = "";
        public string NestedOpacity { get; set; } = "";
        public bool Focusable { get; set; }
        public string Radio { get; set; } = "";
        public string Plain { get; set; } = "";
    }
}
