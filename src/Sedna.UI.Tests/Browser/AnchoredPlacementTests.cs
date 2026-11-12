using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every panel anchored to a trigger opens on the side <c>--panel-placement</c> names,
/// lined up with the trigger, and turns round where that side has no room.
/// </summary>
/// <remarks>
/// <para>
/// Measured in a layout engine, per panel and per placement, in both directions: the
/// panel's box against its trigger's. The token is set two elements above the panel's
/// container, on a stand-in for a bar, so every case also proves that it inherits — the
/// bar pinned to the foot of a pane sets it once.
/// </para>
/// <para>
/// A <c>.form-select</c>'s drop-down is a <c>::picker</c>, which has no box a script can
/// read, so its options stand in for it: they sit inside the panel's padding and
/// border, a few pixels in from each edge.
/// </para>
/// </remarks>
public class AnchoredPlacementTests : ScriptTestBase
{
    public static TheoryData<string, string, string> Cases()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var panel in new[] { "menu", "menu-start", "popover", "combo", "mention", "select" })
            foreach (var placement in new[] { "block-end", "block-start", "inline-end", "inline-start" })
                foreach (var dir in new[] { "ltr", "rtl" })
                    data.Add(panel, placement, dir);
        return data;
    }

    /// <summary>The trigger (<c>#t</c>) and the panel (<c>#p</c>) for one kind of panel.</summary>
    private static string Markup(string panel) => panel switch
    {
        "menu" or "menu-start" => $"""
            <div class="menu-anchor">
              <button class="btn" id="t" type="button" data-menu-toggle aria-expanded="false">Actions</button>
              <div class="menu{(panel == "menu-start" ? " menu--start" : "")}" id="p" hidden>
                <button class="menu-item" type="button">Reassign the order</button>
                <button class="menu-item" type="button">Snooze</button>
              </div>
            </div>
            """,
        "popover" => """
            <button class="btn" id="t" type="button" popovertarget="p">Why?</button>
            <div class="popover" id="p" popover>Priority 2 carries a four-hour response target.</div>
            """,
        "combo" => """
            <div class="form-combo" style="width:160px">
              <div class="form-combo-box" id="t">
                <input class="form-combo-input" type="text" role="combobox" aria-expanded="false" aria-controls="l" aria-label="Product">
              </div>
              <div class="form-combo-panel" id="p">
                <div class="form-combo-list" id="l" role="listbox" aria-label="Products">
                  <div class="form-combo-option" role="option" aria-selected="false">Chai</div>
                  <div class="form-combo-option" role="option" aria-selected="false">Chang</div>
                </div>
              </div>
            </div>
            """,
        "mention" => """
            <div class="mention-field" id="t" style="width:160px">
              <textarea class="form-input" rows="1" aria-label="Note"></textarea>
              <div class="menu" id="p" hidden>
                <button class="menu-item" type="button">Alex Fischer</button>
                <button class="menu-item" type="button">Priya Nair</button>
              </div>
            </div>
            """,
        "select" => """
            <select class="form-select" id="t" style="width:140px" aria-label="Warehouse">
              <option>North depot</option><option>South depot</option><option>Returns desk</option>
            </select>
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(panel)),
    };

    private static string Open(string panel) => panel switch
    {
        "menu" or "menu-start" or "popover" or "select" => "document.getElementById('t').click()",
        "combo" => "document.querySelector('.form-combo-input').setAttribute('aria-expanded', 'true')",
        "mention" => "document.getElementById('p').hidden = false",
        _ => throw new ArgumentOutOfRangeException(nameof(panel)),
    };

    /// <summary>
    /// The trigger's and the panel's boxes, from the trigger's point of view in the
    /// reading direction: [gap below, gap above, gap after, gap before, start-edge offset,
    /// end-edge offset, top offset, bottom offset].
    /// </summary>
    private async Task<double[]> Measure(string panel, string placement, string dir, string where)
    {
        var (page, errors) = await OpenStyled($"""
            <div style="--panel-placement: {placement}">
              <div>
                <div style="position:absolute; {where}">
                  {Markup(panel)}
                </div>
              </div>
            </div>
            """);
        await page.SetViewportSizeAsync(1280, 720);
        // On the root, as an app sets it: a fixed panel's logical area is resolved in its
        // containing block's direction, and that is the viewport's.
        await Js.Run(page, $"document.documentElement.dir = '{dir}'");
        if (panel == "select")
            await page.Locator("#t").ClickAsync();
        else
            await Js.Run(page, Open(panel));
        await Js.Frames(page, 3);

        var box = await page.EvaluateAsync<double[]>("""
            rtl => {
              const t = document.getElementById('t').getBoundingClientRect();
              // A select's options stand in for its drop-down, which no script can measure.
              const boxes = [...document.querySelectorAll('select#t option')].map(o => o.getBoundingClientRect());
              const p = boxes.length
                ? { left: Math.min(...boxes.map(b => b.left)), right: Math.max(...boxes.map(b => b.right)),
                    top: Math.min(...boxes.map(b => b.top)), bottom: Math.max(...boxes.map(b => b.bottom)),
                    width: Math.max(...boxes.map(b => b.width)) }
                : document.getElementById('p').getBoundingClientRect();
              if (p.width === 0) throw new Error('the panel did not open');
              const after = rtl ? t.left - p.right : p.left - t.right;
              const before = rtl ? p.left - t.right : t.left - p.right;
              const start = rtl ? t.right - p.right : p.left - t.left;
              const end = rtl ? t.left - p.left : p.right - t.right;
              return [p.top - t.bottom, t.top - p.bottom, after, before, start, end, p.top - t.top, p.bottom - t.bottom];
            }
            """, dir == "rtl");
        Assert.Empty(errors);
        return box;
    }

    /// <summary>How far in from the panel's own edge the measured box sits: the option inside a select's padding.</summary>
    private static double Inset(string panel) => panel == "select" ? 8 : 1;

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_panel_opens_on_the_side_the_token_names(string panel, string placement, string dir)
    {
        if (NoBrowser) return;
        var box = await Measure(panel, placement, dir, "top:300px; inset-inline-start:500px");
        var inset = Inset(panel);
        var name = $"{panel} {placement} {dir}: ";

        switch (placement)
        {
            case "block-end" or "block-start":
                var gap = placement == "block-end" ? box[0] : box[1];
                Assert.True(gap >= 0 && gap <= 8 + inset, name + $"the gap to the trigger is {gap}px.");
                if (panel == "menu")
                    Assert.True(Math.Abs(box[5]) <= inset, name + $"not lined up with the trigger's end edge ({box[5]}px).");
                else
                    Assert.True(box[4] >= 0 && box[4] <= inset, name + $"not lined up with the trigger's start edge ({box[4]}px).");
                break;
            default:
                var side = placement == "inline-end" ? box[2] : box[3];
                Assert.True(side >= 0 && side <= 8 + inset, name + $"the gap to the trigger's side is {side}px.");
                Assert.True(box[6] >= 0 && box[6] <= inset, name + $"not lined up with the trigger's top edge ({box[6]}px).");
                break;
        }
    }

    public static TheoryData<string, string> Flips()
    {
        var data = new TheoryData<string, string>();
        foreach (var panel in new[] { "menu", "popover", "combo", "mention", "select" })
            foreach (var placement in new[] { "block-start", "inline-end", "inline-start" })
                data.Add(panel, placement);
        return data;
    }

    [Theory]
    [MemberData(nameof(Flips))]
    public async Task With_no_room_on_its_side_the_panel_turns_round(string panel, string placement)
    {
        if (NoBrowser) return;
        // Pressed against the edge the placement faces: no room above, or none beside.
        var where = placement switch
        {
            "block-start" => "top:4px; inset-inline-start:500px",
            "inline-end" => "top:300px; inset-inline-end:4px",
            _ => "top:300px; inset-inline-start:4px",
        };
        var box = await Measure(panel, placement, "ltr", where);
        var inset = Inset(panel);
        var name = $"{panel} {placement}: ";

        if (placement == "block-start")
        {
            Assert.True(box[0] >= 0 && box[0] <= 8 + inset, name + $"did not turn downward; the gap below the trigger is {box[0]}px.");
        }
        else
        {
            var side = placement == "inline-end" ? box[3] : box[2];
            Assert.True(side >= 0 && side <= 8 + inset, name + $"did not turn to the trigger's other side; the gap there is {side}px.");
        }
    }

    [Theory]
    [InlineData("popover", "inline-end")]
    [InlineData("popover", "inline-start")]
    [InlineData("mention", "inline-end")]
    [InlineData("mention", "inline-start")]
    public async Task With_no_room_on_either_side_a_side_panel_opens_below(string panel, string placement)
    {
        if (NoBrowser) return;
        // A popover and a mention list stay anchored on a narrow screen, where a menu and a
        // combo field are sheets. In the middle of a 600px window neither side holds 320px.
        var (page, errors) = await OpenStyled($"""
            <div style="--panel-placement: {placement}">
              <div style="position:absolute; top:200px; left:250px">{Markup(panel).Replace("class=\"menu\" id=\"p\"", "class=\"menu\" id=\"p\" style=\"min-width:320px\"", StringComparison.Ordinal)}</div>
            </div>
            """);
        await page.SetViewportSizeAsync(600, 700);
        await Js.Run(page, Open(panel));
        await Js.Frames(page, 3);
        var box = await page.EvaluateAsync<double[]>("""
            () => {
              const t = document.getElementById('t').getBoundingClientRect();
              const p = document.getElementById('p').getBoundingClientRect();
              return [p.top - t.bottom, p.left, innerWidth - p.right];
            }
            """);
        Assert.True(box[0] >= 0 && box[0] <= 8, $"{panel} {placement}: not below the trigger; the gap is {box[0]}px.");
        Assert.True(box[1] >= 0 && box[2] >= 0, $"{panel} {placement}: off the screen ({box[1]}px, {box[2]}px).");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Below_768px_an_upward_menu_is_still_a_sheet()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="--panel-placement: block-start; margin-top:300px">
              <div class="menu-anchor">
                <button class="btn" id="t" type="button" data-menu-toggle aria-expanded="false">Actions</button>
                <div class="menu" id="p" hidden><button class="menu-item" type="button">Snooze</button></div>
              </div>
            </div>
            """);
        await page.SetViewportSizeAsync(375, 700);
        await page.Locator("#t").ClickAsync();
        await Js.Frames(page);
        var edges = await page.EvaluateAsync<double[]>("""
            () => {
              const r = document.getElementById('p').getBoundingClientRect();
              return [r.left, document.documentElement.clientWidth - r.right, innerHeight - r.bottom];
            }
            """);
        Assert.Equal(0, edges[0], 0);
        Assert.Equal(0, edges[1], 0);
        Assert.Equal(0, edges[2], 0);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_placement_set_nearer_the_panel_wins_over_the_bars()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="--panel-placement: block-start; position:absolute; top:300px; left:500px">
              <div class="menu-anchor" style="--panel-placement: block-end">
                <button class="btn" id="t" type="button" data-menu-toggle aria-expanded="false">Actions</button>
                <div class="menu" id="p" hidden><button class="menu-item" type="button">Snooze</button></div>
              </div>
            </div>
            """);
        await page.Locator("#t").ClickAsync();
        await Js.Frames(page);
        var below = await page.EvaluateAsync<double>(
            "() => document.getElementById('p').getBoundingClientRect().top - document.getElementById('t').getBoundingClientRect().bottom");
        Assert.InRange(below, 0, 8);
        Assert.Empty(errors);
    }
}
