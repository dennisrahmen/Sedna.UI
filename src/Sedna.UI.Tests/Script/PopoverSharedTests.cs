using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// One <c>.popover</c> shared by many <c>data-popover</c> triggers: it opens at whichever trigger
/// the reader reached, a panel with <c>data-popover-for</c> is filled by the app for that trigger
/// before it opens, and a hover card does not open over another open popover it would close.
/// </summary>
public class PopoverSharedTests : ScriptTestBase
{
    private const string Rows = """
        <div style="padding:60px 80px">
          <p><span id="a" data-popover="card" data-popover-hover tabindex="0">ORD-4182</span></p>
          <p style="margin-top:160px"><span id="b" data-popover="card" data-popover-hover tabindex="0">ORD-4183</span></p>
          <p style="margin-top:160px"><button type="button" id="other-open" popovertarget="picker">Month</button></p>
        </div>
        <div class="popover" id="card" popover><p id="card-text">Order detail</p></div>
        <div class="popover" id="picker" popover>
          <p>November</p>
          <span id="inner" data-popover="card" data-popover-hover tabindex="0">ORD-4190</span>
        </div>
        """;

    private static Task<bool> IsOpen(IPage page, string id) =>
        page.EvaluateAsync<bool>("id => document.getElementById(id).matches(':popover-open')", id);

    // How far the card's top sits below the trigger's bottom: small when it is anchored there.
    private static Task<double> GapUnder(IPage page, string trigger) =>
        page.EvaluateAsync<double>("""
            id => document.getElementById('card').getBoundingClientRect().top
                - document.getElementById(id).getBoundingClientRect().bottom
            """, trigger);

    [Fact]
    public async Task A_card_open_at_one_trigger_moves_to_the_next_one_the_pointer_rests_on()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Rows, clockPausedAt: new DateTime(2030, 1, 1));

        await page.HoverAsync("#a");
        await page.Clock.RunForAsync(400);
        Assert.True(await IsOpen(page, "card"));
        Assert.InRange(await GapUnder(page, "a"), 0, 16);

        await page.HoverAsync("#b");
        await page.Clock.RunForAsync(400);
        Assert.True(await IsOpen(page, "card"));
        Assert.InRange(await GapUnder(page, "b"), 0, 16);
        await Assertions.Expect(page.Locator("#b")).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.Locator("#a")).ToHaveAttributeAsync("aria-expanded", "false");

        // The trigger it left can go: the card is no longer about it.
        await Js.Run(page, "() => document.getElementById('a').remove()");
        await page.Clock.RunForAsync(50);   // frames run on the paused clock
        Assert.True(await IsOpen(page, "card"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_click_on_another_trigger_moves_the_card_and_a_click_on_its_own_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="padding:60px 80px">
              <p><span id="a" data-popover="card" role="button" tabindex="0">5</span></p>
              <p style="margin-top:160px"><span id="b" data-popover="card" role="button" tabindex="0">3</span></p>
            </div>
            <div class="popover" id="card" popover><p>Invoices</p></div>
            """);

        await page.ClickAsync("#a");
        Assert.InRange(await GapUnder(page, "a"), 0, 16);
        await page.ClickAsync("#b");
        Assert.True(await IsOpen(page, "card"));
        Assert.InRange(await GapUnder(page, "b"), 0, 16);
        await page.ClickAsync("#b");
        Assert.False(await IsOpen(page, "card"));

        await page.FocusAsync("#a");
        await page.Keyboard.PressAsync("Enter");
        Assert.InRange(await GapUnder(page, "a"), 0, 16);
        await page.FocusAsync("#b");
        await page.Keyboard.PressAsync("Enter");
        Assert.True(await IsOpen(page, "card"));
        Assert.InRange(await GapUnder(page, "b"), 0, 16);
        Assert.Empty(errors);
    }

    // The app's fill: records what was asked, and answers after a delay the test controls —
    // as a Blazor render does, some time after the event.
    private const string Filler = """
        <script>
          window.asked = [];
          document.addEventListener('sedna-popover-fill', e => {
            window.asked.push(e.target.id + ':' + e.detail.trigger + ':' + e.detail.popover);
            window.answer = () => {
              const card = document.getElementById('card');
              document.getElementById('card-text').textContent = 'Detail of ' + e.detail.trigger;
              card.setAttribute('data-popover-for', e.detail.trigger);
            };
          });
        </script>
        """;

    [Fact]
    public async Task A_card_filled_per_trigger_asks_first_and_opens_when_the_app_has_filled_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            Rows.Replace("id=\"card\" popover", "id=\"card\" popover data-popover-for=\"\"", StringComparison.Ordinal) + Filler,
            clockPausedAt: new DateTime(2030, 1, 1));

        await page.HoverAsync("#a");
        await page.Clock.RunForAsync(400);
        Assert.Equal(["a:a:card"], await page.EvaluateAsync<string[]>("() => window.asked"));
        Assert.False(await IsOpen(page, "card"));                          // not before it is filled

        await Js.Run(page, "() => window.answer()");
        await page.Clock.RunForAsync(50);   // frames run on the paused clock
        Assert.True(await IsOpen(page, "card"));
        Assert.Equal("Detail of a", await page.TextContentAsync("#card-text"));
        Assert.InRange(await GapUnder(page, "a"), 0, 16);

        // Asked for the next trigger, it stays where it is until that one is filled too.
        await page.HoverAsync("#b");
        await page.Clock.RunForAsync(400);
        Assert.InRange(await GapUnder(page, "a"), 0, 16);
        await Js.Run(page, "() => window.answer()");
        await page.Clock.RunForAsync(50);   // frames run on the paused clock
        Assert.InRange(await GapUnder(page, "b"), 0, 16);
        Assert.Equal("Detail of b", await page.TextContentAsync("#card-text"));

        // Back to one it holds already: no wait.
        await page.Mouse.MoveAsync(5, 5);
        await page.Clock.RunForAsync(400);
        Assert.False(await IsOpen(page, "card"));
        await page.HoverAsync("#b");
        await page.Clock.RunForAsync(400);
        Assert.True(await IsOpen(page, "card"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_fill_that_arrives_after_the_pointer_has_left_opens_nothing()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            Rows.Replace("id=\"card\" popover", "id=\"card\" popover data-popover-for=\"\"", StringComparison.Ordinal) + Filler,
            clockPausedAt: new DateTime(2030, 1, 1));

        await page.HoverAsync("#a");
        await page.Clock.RunForAsync(400);
        await page.Mouse.MoveAsync(5, 5);
        await page.Clock.RunForAsync(400);

        await Js.Run(page, "() => window.answer()");
        await page.Clock.RunForAsync(50);   // frames run on the paused clock
        Assert.False(await IsOpen(page, "card"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_hover_card_waits_while_another_popover_is_open_and_opens_from_inside_one()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Rows, clockPausedAt: new DateTime(2030, 1, 1));

        await page.ClickAsync("#other-open");
        Assert.True(await IsOpen(page, "picker"));

        // Resting on a trigger outside it would close it: nothing opens.
        await Js.Run(page, "() => document.getElementById('a').focus()");
        await page.Clock.RunForAsync(400);
        Assert.False(await IsOpen(page, "card"));
        Assert.True(await IsOpen(page, "picker"));

        // A trigger inside it opens the card as its child, and it stays open.
        await Js.Run(page, "() => document.getElementById('inner').focus()");
        await page.Clock.RunForAsync(400);
        Assert.True(await IsOpen(page, "card"));
        Assert.True(await IsOpen(page, "picker"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_pinned_card_holds_off_another_and_stays_where_it_was_pinned()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Rows + """
            <p style="position:absolute;top:20px;left:600px"><span id="c" data-popover="other-card" data-popover-hover tabindex="0">Carrier</span></p>
            <div class="popover" id="other-card" popover><p>Northwind Retail</p></div>
            """, clockPausedAt: new DateTime(2030, 1, 1));

        await page.ClickAsync("#a");                                       // pins it at a
        Assert.True(await IsOpen(page, "card"));
        await page.HoverAsync("#b");
        await page.Clock.RunForAsync(400);
        Assert.InRange(await GapUnder(page, "a"), 0, 16);                  // pinned: it does not follow

        await page.HoverAsync("#c");
        await page.Clock.RunForAsync(400);
        Assert.False(await IsOpen(page, "other-card"));                    // and nothing closes it
        Assert.True(await IsOpen(page, "card"));
        Assert.Empty(errors);
    }
}
