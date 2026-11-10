using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A field of checkboxes, measured: a fieldset that draws no browser frame and keeps its legend in
/// the field's column, a dependent list that keeps its indent past a message and its state with
/// <c>--off</c>, a long list held to its height, items whose box sits on the name's line, and the
/// select-all, count and exclusive behaviour of <c>21-checks.js</c>.
/// </summary>
public class CheckListLayoutTests : ScriptTestBase
{
    private const string Field = """
        <div style="padding:20px; width:480px; display:flex; flex-direction:column; gap:24px">
          <fieldset class="form-field" id="set">
            <legend class="form-label" id="set-legend">Run on</legend>
            <p class="form-hint" id="set-hint">Where the job starts.</p>
            <div class="form-check-list" id="list">
              <label class="form-check" id="one"><input type="checkbox" name="agents" value="a" />
                <span class="choice-text"><span id="one-name">build-runner-01</span><span class="choice-meta">198.51.100.11</span><span class="choice-hint">Offline since 06:40.</span></span></label>
              <label class="form-check" id="two"><input type="checkbox" name="agents" value="b" /> <span id="two-name">build-runner-02</span></label>
            </div>
            <div class="form-check-list" id="busy" aria-busy="true">
              <div class="form-check-skeleton" id="bone"><span class="skeleton"></span><span class="skeleton"></span></div>
            </div>
          </fieldset>
          <div class="form-field" id="plain">
            <label class="form-label" id="plain-label">Run on</label>
            <p class="form-hint" id="plain-hint">Where the job starts.</p>
          </div>
        </div>
        """;

    [Fact]
    public async Task A_field_fieldset_has_no_browser_frame_and_its_legend_sits_in_the_column_as_a_label_does()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field);

        var failures = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [];
                const r = id => document.getElementById(id).getBoundingClientRect();
                const s = getComputedStyle(document.getElementById('set'));
                for (const side of ['Top', 'Right', 'Bottom', 'Left']) {
                    if (parseFloat(s['border' + side + 'Width']) !== 0) out.push(`the fieldset has a ${side.toLowerCase()} border`);
                    if (parseFloat(s['padding' + side]) !== 0) out.push(`the fieldset has ${side.toLowerCase()} padding`);
                }
                if (parseFloat(s.marginInlineStart) !== 0 || parseFloat(s.marginInlineEnd) !== 0) out.push('the fieldset has a margin');
                const set = r('set'), legend = r('set-legend');
                if (Math.abs(legend.top - set.top) > 0.5) out.push(`the legend is ${legend.top - set.top}px below the field's top`);
                if (Math.abs(legend.width - set.width) > 0.5) out.push(`the legend is ${legend.width}px wide in a ${set.width}px field`);
                // The hint follows the legend as it follows a label: the same gap.
                const asLegend = r('set-hint').top - legend.bottom, asLabel = r('plain-hint').top - r('plain-label').bottom;
                if (Math.abs(asLegend - asLabel) > 0.5) out.push(`the hint is ${asLegend}px under the legend and ${asLabel}px under a label`);
                const next = r('list').top - r('set-hint').bottom;
                if (Math.abs(next - asLabel) > 0.5) out.push(`the list is ${next}px under the hint, not the field's ${asLabel}px`);
                if (Math.abs(legend.height - r('plain-label').height) > 0.5) out.push(`the legend is ${legend.height}px tall, a label ${r('plain-label').height}px`);
                return out;
            }
            """);

        Assert.True(failures.Length == 0, Engine + ":\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_item_keeps_its_box_on_the_names_line_and_a_loading_row_is_as_tall_as_an_item()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field);

        var failures = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [];
                const r = el => el.getBoundingClientRect();
                const mid = b => b.top + b.height / 2;
                for (const id of ['one', 'two']) {
                    const box = r(document.querySelector('#' + id + ' > input')), name = r(document.getElementById(id + '-name'));
                    if (Math.abs(mid(box) - mid(name)) > 0.5) out.push(`${id}: the box is centred at ${mid(box)}, its name's line at ${mid(name)}`);
                }
                const item = r(document.getElementById('two')), bone = r(document.getElementById('bone'));
                if (Math.abs(item.height - bone.height) > 0.5) out.push(`a loading row is ${bone.height}px tall, an item ${item.height}px`);
                const label = r(document.getElementById('one')), text = r(document.querySelector('#one .choice-text'));
                if (label.right > text.right + 0.5) out.push('an item is wider than its text, so a click beside it picks it');
                return out;
            }
            """);

        Assert.True(failures.Length == 0, Engine + ":\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    private const string Dependents = """
        <div style="padding:20px; width:520px; display:flex; flex-direction:column; gap:12px">
          <label class="switch"><input type="checkbox" id="sw" checked /> <span id="sw-label">Only on some agents</span></label>
          <p class="form-error" id="sw-error"><i class="ri-error-warning-line"></i><span>Pick at least one agent.</span></p>
          <div class="choice-dependent choice-dependent--hide" id="sw-dep"><input class="form-input" /></div>

          <label class="form-check"><input type="checkbox" id="ck" checked /> <span id="ck-label">Notify</span></label>
          <p class="form-warning" id="ck-warn"><i class="ri-alert-line"></i><span>Nobody is on call tonight.</span></p>
          <div class="choice-dependent choice-dependent--dim" id="ck-dep"><input class="form-input" id="ck-input" /></div>

          <label class="form-check"><input type="checkbox" id="every" /> <span id="every-label">Every agent</span></label>
          <div class="choice-dependent choice-dependent--off choice-dependent--dim" id="every-dep">
            <div class="form-check-list"><label class="form-check"><input type="checkbox" id="every-item" name="ev" /> <span>build-runner-01</span></label></div>
          </div>
          <label class="form-check"><input type="checkbox" id="every-2" /> <span>Every group</span></label>
          <div class="choice-dependent choice-dependent--off choice-dependent--hide" id="every-2-dep"><input class="form-input" /></div>
        </div>
        """;

    [Fact]
    public async Task A_message_between_an_option_and_its_block_keeps_the_indent_and_the_state()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dependents);

        const string Starts = """
            () => ['sw-dep', 'sw-label', 'ck-dep', 'ck-label'].map(id => document.getElementById(id).getBoundingClientRect().left)
            """;
        var starts = await page.EvaluateAsync<double[]>(Starts);
        Assert.Equal(starts[1], starts[0], 0.01);
        Assert.Equal(starts[3], starts[2], 0.01);

        await page.UncheckAsync("#sw");
        await page.UncheckAsync("#ck");
        var off = await page.EvaluateAsync<string[]>("""
            () => [getComputedStyle(document.getElementById('sw-dep')).display,
                   getComputedStyle(document.getElementById('ck-dep')).opacity,
                   String(document.getElementById('ck-dep').inert)]
            """);
        Assert.Equal(["none", "0.45", "true"], off);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_off_dependent_applies_while_its_option_is_off()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dependents);

        const string State = """
            () => {
                const input = document.getElementById('every-item');
                input.focus();
                const focusable = document.activeElement === input;
                input.blur();
                return [getComputedStyle(document.getElementById('every-dep')).opacity,
                        String(focusable),
                        getComputedStyle(document.getElementById('every-2-dep')).display];
            }
            """;
        Assert.Equal(["1", "true", "flex"], await page.EvaluateAsync<string[]>(State));
        var start = await page.EvaluateAsync<double[]>(
            "() => ['every-dep', 'every-label'].map(id => document.getElementById(id).getBoundingClientRect().left)");
        Assert.Equal(start[1], start[0], 0.01);

        await page.CheckAsync("#every");
        await page.CheckAsync("#every-2");
        Assert.Equal(["0.45", "false", "none"], await page.EvaluateAsync<string[]>(State));

        await page.UncheckAsync("#every");
        Assert.Equal("1", (await page.EvaluateAsync<string[]>(State))[0]);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", 256)]
    [InlineData("style=\"--check-list-max: 10rem\"", 160)]
    public async Task A_long_list_is_held_to_its_height_and_scrolls(string style, double height)
    {
        if (NoBrowser) return;
        var rows = string.Concat(Enumerable.Range(1, 30).Select(i =>
            $"""<label class="form-check"><input type="checkbox" name="hosts" value="{i}" /> <span>build-runner-{i:00}</span></label>"""));
        var (page, errors) = await OpenStyled($"""
            <div style="padding:20px; width:420px">
              <div class="form-check-list form-check-list--scroll" id="long" {style}>{rows}</div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => { const el = document.getElementById('long');
                    return [el.getBoundingClientRect().height, el.scrollHeight, el.clientHeight,
                            parseFloat(getComputedStyle(document.documentElement).fontSize)]; }
            """);
        Assert.Equal(height / 16 * m[3], m[0], 0.5);
        Assert.True(m[1] > m[2] + 100, $"The list does not scroll: {m[1]}px of content in {m[2]}px.");

        // The last item, reached by the keyboard, is brought into the box.
        await page.FocusAsync("#long label:last-child input");
        var visible = await page.EvaluateAsync<bool>("""
            () => { const box = document.getElementById('long').getBoundingClientRect();
                    const item = document.querySelector('#long label:last-child').getBoundingClientRect();
                    return item.top >= box.top - 0.5 && item.bottom <= box.bottom + 0.5; }
            """);
        Assert.True(visible, "The focused last item is outside the scrolled box.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_rejected_box_and_a_ticked_card_take_their_edges()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="padding:20px; width:420px; display:flex; flex-direction:column; gap:12px">
              <label class="form-check"><input type="checkbox" id="bad" aria-invalid="true" /> <span>Operations</span></label>
              <label class="form-check"><input type="checkbox" id="bad-on" aria-invalid="true" checked /> <span>Finance</span></label>
              <label class="form-check"><input type="radio" id="bad-radio" aria-invalid="true" /> <span>Weekly</span></label>
              <div class="form-check-list form-check-list--cards">
                <label class="form-check" id="card-on"><input type="checkbox" checked /> <span>Orders</span></label>
                <label class="form-check" id="card-off"><input type="checkbox" /> <span>Returns</span></label>
              </div>
            </div>
            """, "<style>*, *::before, *::after { transition: none !important; }</style>");

        var failures = await page.EvaluateAsync<string[]>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = token => { probe.style.color = `var(${token})`; return getComputedStyle(probe).color; };
                const edge = id => getComputedStyle(document.getElementById(id)).borderTopColor;
                const out = [];
                const danger = resolve('--field-border-danger'), selected = resolve('--selected');
                for (const id of ['bad', 'bad-on', 'bad-radio']) if (edge(id) !== danger) out.push(`${id}: edged ${edge(id)}, not ${danger}`);
                if (edge('card-on') !== selected) out.push(`a ticked card is edged ${edge('card-on')}, not ${selected}`);
                if (edge('card-off') === selected) out.push('an unticked card is edged as a ticked one');
                const card = document.getElementById('card-off').getBoundingClientRect(), list = document.querySelector('.form-check-list--cards').getBoundingClientRect();
                if (Math.abs(card.width - list.width) > 0.5) out.push(`a card is ${card.width}px wide in a ${list.width}px list`);
                return out;
            }
            """);

        Assert.True(failures.Length == 0, Engine + ":\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    private const string Behaviour = """
        <form id="f" style="padding:20px; width:420px">
          <fieldset class="form-field">
            <legend class="form-label">Agents</legend>
            <div class="form-check-list-head">
              <label class="form-check"><input type="checkbox" id="all" data-check-all="agents" /> <span>Every agent</span></label>
              <span class="form-check-list-count" id="count" data-check-count="agents" data-check-format="{n} of {m} selected">0 of 4 selected</span>
            </div>
            <div class="form-check-list">
              <label class="form-check"><input type="checkbox" name="agents" value="1" id="a1" checked /> <span>build-runner-01</span></label>
              <label class="form-check"><input type="checkbox" name="agents" value="2" id="a2" /> <span>build-runner-02</span></label>
              <label class="form-check"><input type="checkbox" name="agents" value="3" id="a3" disabled /> <span>build-runner-03</span></label>
              <label class="form-check"><input type="checkbox" name="agents" value="4" id="a4" aria-readonly="true" /> <span>build-runner-04</span></label>
              <p class="form-check-or">or</p>
              <label class="form-check"><input type="checkbox" name="agents" value="none" id="none" data-check-exclusive /> <span>None of these</span></label>
            </div>
          </fieldset>
          <fieldset class="form-field">
            <legend class="form-label">Other agents</legend>
            <label class="form-check"><input type="checkbox" name="agents" value="9" id="other" /> <span>build-runner-09</span></label>
          </fieldset>
        </form>
        """;

    private const string Read = """
        () => ({
            all: document.getElementById('all').checked,
            mixed: document.getElementById('all').indeterminate,
            count: document.getElementById('count').textContent,
            ticked: ['a1', 'a2', 'a3', 'a4', 'none', 'other'].filter(id => document.getElementById(id).checked).join(' '),
            changes: window.changes.join(' '),
        })
        """;

    private sealed record Checks
    {
        public bool All { get; init; }
        public bool Mixed { get; init; }
        public string Count { get; init; } = "";
        public string Ticked { get; init; } = "";
        public string Changes { get; init; } = "";

        public Checks() { }

        public Checks(bool all, bool mixed, string count, string ticked, string changes) =>
            (All, Mixed, Count, Ticked, Changes) = (all, mixed, count, ticked, changes);
    }

    [Fact]
    public async Task Select_all_follows_its_items_and_the_count_says_how_many()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Behaviour);
        await Js.Run(page, "() => { window.changes = []; document.addEventListener('change', e => window.changes.push(e.target.id)); }");

        // The page arrives with one of four ticked: half ticked, and counted.
        var s = await page.EvaluateAsync<Checks>(Read);
        Assert.Equal(new Checks(false, true, "1 of 4 selected", "a1", ""), s);

        // Ticking it ticks every item that can change, and each one says so; one that is
        // disabled or read-only stays as it was, so the box stays half ticked.
        await page.ClickAsync("#all");
        s = await page.EvaluateAsync<Checks>(Read);
        Assert.Equal("a1 a2", s.Ticked);
        Assert.Equal("a2 all", s.Changes);
        Assert.Equal("2 of 4 selected", s.Count);

        // Every changeable item ticked: ticked. Clearing it clears them.
        await Js.Run(page, "() => { document.getElementById('a4').removeAttribute('aria-readonly'); window.changes = []; }");
        await page.ClickAsync("#a4");
        s = await page.EvaluateAsync<Checks>(Read);
        Assert.True(s.All);
        Assert.False(s.Mixed);
        Assert.Equal("3 of 4 selected", s.Count);

        await page.ClickAsync("#all");
        s = await page.EvaluateAsync<Checks>(Read);
        Assert.Equal(new Checks(false, false, "0 of 4 selected", "", "a4 a1 a2 a4 all"), s);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_exclusive_option_clears_the_rest_and_is_cleared_by_them()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Behaviour);
        await Js.Run(page, "() => { window.changes = []; document.addEventListener('change', e => window.changes.push(e.target.id)); }");

        await page.ClickAsync("#none");
        var s = await page.EvaluateAsync<Checks>(Read);
        Assert.Equal("none", s.Ticked);
        Assert.Equal("a1 none", s.Changes);
        Assert.False(s.Mixed);
        Assert.Equal("0 of 4 selected", s.Count);

        await page.ClickAsync("#a2");
        s = await page.EvaluateAsync<Checks>(Read);
        Assert.Equal("a2", s.Ticked);

        // Select all clears it too; a box of the same name in another fieldset is never touched.
        await page.ClickAsync("#none");
        await page.ClickAsync("#other");
        await page.ClickAsync("#all");
        s = await page.EvaluateAsync<Checks>(Read);
        Assert.Equal("a1 a2 other", s.Ticked);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_box_the_app_renders_as_mixed_is_indeterminate_to_match()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <label class="form-check"><input type="checkbox" id="m" aria-checked="mixed" /> <span>Every warehouse</span></label>
            <label class="form-check"><input type="checkbox" id="p" /> <span>Every team</span></label>
            """);
        const string State = "() => [document.getElementById('m').indeterminate, document.getElementById('p').indeterminate]";

        var state = await page.EvaluateAsync<bool[]>(State);
        Assert.Equal(new[] { true, false }, state);
        await Js.Run(page, "() => document.getElementById('m').setAttribute('aria-checked', 'true')");
        await page.WaitForFunctionAsync("() => !document.getElementById('m').indeterminate");
        await Js.Run(page, "() => document.getElementById('p').setAttribute('aria-checked', 'mixed')");
        await page.WaitForFunctionAsync("() => document.getElementById('p').indeterminate");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Items_added_by_a_render_are_counted()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Behaviour);

        await Js.Run(page, """
            () => {
                const row = document.getElementById('a2').closest('label').cloneNode(true);
                row.querySelector('input').id = 'a5';
                row.querySelector('input').checked = true;
                document.getElementById('a2').closest('.form-check-list').prepend(row);
            }
            """);
        await page.WaitForFunctionAsync("() => document.getElementById('count').textContent === '2 of 5 selected'");
        Assert.Empty(errors);
    }
}
