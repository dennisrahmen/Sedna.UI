using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every family that draws a field's edge draws every field state (46-form-layout.css, "Field
/// states"): rejected (<c>aria-invalid="true"</c>) and unusual (<c>data-state="warn"</c>), at rest
/// and focused, at every size, in both variants — with the attribute on the control, where a screen
/// reader reads it, and the frame around it following.
/// </summary>
/// <remarks>
/// <c>.input-group</c> was the family that lacked it: the input inside gives up its own edge to the
/// group, so a rejected value was announced and looked perfectly normal. The families are found in
/// the stylesheet — every rule that draws <c>var(--field-border)</c> — so the next one is held here
/// the moment it exists.
/// </remarks>
public class FieldStateTests : ScriptTestBase
{
    /// <summary>
    /// One fixture per family and size. <c>data-control</c> takes the state, <c>data-edge</c> draws
    /// it, <c>data-focus</c> is focused when that is not the control, and <c>data-no-ring</c> marks a
    /// frame whose control draws its own ring.
    /// </summary>
    private static readonly (string Family, string Markup)[] Fixtures =
    [
        ("form-input", """<input class="form-input" data-control data-edge aria-label="Retention" value="400">"""),
        ("form-input", """<input class="form-input form-input--sm" data-control data-edge aria-label="Retention" value="400">"""),
        ("form-input", """<input class="form-input form-input--lg" data-control data-edge aria-label="Retention" value="400">"""),
        ("form-input", """<textarea class="form-input" data-control data-edge aria-label="Reason" rows="2"></textarea>"""),
        ("form-select", """<select class="form-select" data-control data-edge aria-label="Team"><option>Fulfilment</option></select>"""),
        ("form-select", """<select class="form-select form-input--sm" data-control data-edge aria-label="Team"><option>Fulfilment</option></select>"""),
        ("form-select", """<select class="form-select form-input--lg" data-control data-edge aria-label="Team"><option>Fulfilment</option></select>"""),
        ("input-group", """<div class="input-group" data-edge><input class="form-input" data-control aria-label="Go-live" value="31.02.26"><button class="btn" type="button" aria-label="Pick a date"><i class="ri-calendar-line"></i></button></div>"""),
        ("input-group", """<div class="input-group input-group--sm" data-edge><span class="input-affix"><i class="ri-search-line"></i></span><input class="form-input" data-control aria-label="Filter"></div>"""),
        ("input-group", """<div class="input-group input-group--lg" data-edge><input class="form-input" data-control aria-label="Price" value="1200"><span class="input-affix">EUR</span></div>"""),
        ("input-group", """<div class="input-group" data-edge><select class="form-select" data-control aria-label="Unit"><option>days</option></select><span class="input-affix">per year</span></div>"""),
        ("form-combo-box", """<div class="form-combo-box" data-edge><input class="form-combo-input" data-control aria-label="Recipients"></div>"""),
        ("form-color", """<input type="color" class="form-color" data-control data-edge aria-label="Accent" value="#3aa8e2">"""),
        ("form-color", """<input type="color" class="form-color form-input--sm" data-control data-edge aria-label="Accent" value="#3aa8e2">"""),
        ("form-color", """<input type="color" class="form-color form-input--lg" data-control data-edge aria-label="Accent" value="#3aa8e2">"""),
        ("editor", """<div class="editor" data-edge><div class="editor-toolbar" role="toolbar" aria-label="Formatting"></div><div class="editor-body prose"><div contenteditable="true" role="textbox" aria-label="Summary" data-control></div></div></div>"""),
        ("editor", """<div class="editor" data-edge><div class="editor-body prose" tabindex="0" data-focus></div><textarea data-editor-value hidden data-control></textarea></div>"""),
        ("md-editor", """<div class="md-editor" data-edge data-no-ring><textarea class="form-input md-input" data-md-input data-control aria-label="Reason" rows="3"></textarea></div>"""),
    ];

    /// <summary>
    /// They draw a field's edge and hold no value to judge: the frame's search box and nav filter,
    /// and a read-only value shown in the shape of a field. And a signature pad, which takes no
    /// focus — nothing in it is operated from the keyboard — so it has no focused state to hold;
    /// <c>SignatureTests</c> holds its edge to the field states at rest.
    /// </summary>
    private static readonly string[] Exempt = ["nav-filter-input", "search-input", "form-value-display", "form-signature-pad"];

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Every_field_family_draws_every_state_at_rest_and_in_focus(string variant)
    {
        if (NoBrowser) return;
        var body = string.Concat(Fixtures.Select((f, i) =>
            $"""<div data-fixture="{f.Family} #{i}" style="margin:8px 0; max-width:360px">{f.Markup}</div>"""));
        var (page, errors) = await OpenStyled(body,
            // A transition would hand back a colour on its way there.
            "<style>*, *::before, *::after { transition: none !important; }</style>");
        await page.EvaluateAsync<string>("v => document.documentElement.dataset.variant = v", variant);

        var failures = await page.EvaluateAsync<string[]>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = token => { probe.style.color = `var(${token})`; return getComputedStyle(probe).color; };
                const want = { warn: resolve('--field-border-warn'), invalid: resolve('--field-border-danger'), ring: resolve('--focus-ring') };
                const out = [];
                for (const fx of document.querySelectorAll('[data-fixture]')) {
                    const edge = fx.querySelector('[data-edge]'), control = fx.querySelector('[data-control]');
                    const focus = fx.querySelector('[data-focus]') || control;
                    const name = fx.dataset.fixture;
                    const resting = getComputedStyle(edge).borderTopColor;
                    for (const state of ['warn', 'invalid', 'both']) {
                        control.removeAttribute('aria-invalid');
                        control.removeAttribute('data-state');
                        if (state !== 'invalid') control.setAttribute('data-state', 'warn');
                        if (state !== 'warn') control.setAttribute('aria-invalid', 'true');
                        const expected = state === 'warn' ? want.warn : want.invalid;

                        document.activeElement && document.activeElement.blur();
                        const rest = getComputedStyle(edge).borderTopColor;
                        if (rest !== expected) out.push(`${name}, ${state}, at rest: the edge is ${rest}, not ${expected}`);
                        if (rest === resting) out.push(`${name}, ${state}, at rest: the edge did not change`);

                        focus.focus();
                        if (!focus.matches(':focus')) { out.push(`${name}: could not be focused`); continue; }
                        const style = getComputedStyle(edge);
                        if (style.borderTopColor !== expected) out.push(`${name}, ${state}, focused: the edge is ${style.borderTopColor}, not ${expected}`);
                        if (!fx.querySelector('[data-no-ring]') && !style.boxShadow.includes(want.ring))
                            out.push(`${name}, ${state}, focused: no focus ring (${style.boxShadow})`);
                    }
                    control.removeAttribute('aria-invalid');
                    control.removeAttribute('data-state');
                }
                return out;
            }
            """);

        Assert.True(failures.Length == 0, $"{Engine}, {variant}:\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    [Fact]
    public void Every_family_that_draws_a_field_edge_has_a_state_fixture()
    {
        var css = Assets.StripComments(Assets.Css);
        var drawers = Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"border\s*:\s*1px solid var\(--field-border\)"))
            .SelectMany(m => m.Groups["sel"].Value.Split(','))
            .Select(s => Regex.Match(s.Trim(), @"^\.(?<c>[a-z][a-z0-9-]*)").Groups["c"].Value)
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(drawers);
        var covered = Fixtures.Select(f => f.Family).Concat(Exempt).ToHashSet(StringComparer.Ordinal);
        var missing = drawers.Except(covered).ToList();
        Assert.True(missing.Count == 0,
            "These draw a field's edge and have no state fixture here, so nothing holds them to the field states: ."
            + string.Join(", .", missing));
    }
}
