using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.listbox</c>: the choice and the keyboard behind a <c>role="listbox"</c>, the events it sends, the
/// look of a chosen row, and a transfer between two of them.
/// </summary>
public class ListboxTests : ScriptTestBase
{
    private const string Fixture = """
        <div style="padding:20px; width:360px">
          <div class="listbox" role="listbox" id="multi" aria-label="Groups" aria-multiselectable="true" tabindex="0">
            <div role="option" data-value="a">Alpha</div>
            <div role="option" data-value="b" aria-selected="true">Bravo</div>
            <div role="option" data-value="c" aria-disabled="true">Charlie</div>
            <div role="group" aria-labelledby="g"><div class="listbox-heading" id="g" role="presentation">More</div>
              <div role="option" data-value="d">Delta</div><div role="option" data-value="e">Echo</div></div>
          </div>
          <div class="listbox" role="listbox" id="single" aria-label="One" tabindex="0">
            <div role="option" data-value="x">Xray</div><div role="option" data-value="y">Yankee</div><div role="option" data-value="z">Zulu</div>
          </div>
          <div class="transfer" data-transfer style="margin-top:20px">
            <div class="listbox" role="listbox" id="from" aria-multiselectable="true" aria-label="From" tabindex="0" data-transfer-from>
              <div role="option">Gamma</div><div role="option">Kilo</div><div role="option">Lima</div></div>
            <div class="transfer-actions"><button type="button" data-transfer-add aria-label="Add">+</button></div>
            <div class="listbox" role="listbox" id="to" aria-multiselectable="true" aria-label="To" tabindex="0" data-transfer-to>
              <div role="option">Hotel</div></div>
          </div>
        </div>
        """;

    private static Task<string[]> Rows(IPage page, string id) =>
        page.EvaluateAsync<string[]>("id => [...document.querySelectorAll('#' + id + ' [role=option]')].map(o => o.textContent.trim())", id);

    private static Task<string[]> Values(IPage page, string id) =>
        page.EvaluateAsync<string[]>("id => sednaUi.listbox.values(document.getElementById(id))", id);

    [Fact]
    public async Task A_click_chooses_one_row_and_ctrl_and_shift_extend()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await page.Locator("#multi [data-value=d]").ClickAsync();
        Assert.Equal(["d"], await Values(page, "multi"));
        await page.Locator("#multi [data-value=a]").ClickAsync(new() { Modifiers = [KeyboardModifier.ControlOrMeta] });
        Assert.Equal(["a", "d"], await Values(page, "multi"));
        await page.Locator("#multi [data-value=e]").ClickAsync();
        await page.Locator("#multi [data-value=a]").ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
        // Charlie is disabled and is passed over by the range.
        Assert.Equal(["a", "b", "d", "e"], await Values(page, "multi"));
        await page.Locator("#multi [data-value=c]").ClickAsync(new() { Force = true });
        Assert.Equal(["a", "b", "d", "e"], await Values(page, "multi"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_moves_chooses_and_reaches_every_row_from_one_tab_stop()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await page.Locator("#multi").FocusAsync();
        // The first chosen row is where the keyboard starts.
        Assert.Equal("b", await page.EvaluateAsync<string>("() => document.getElementById(document.getElementById('multi').getAttribute('aria-activedescendant')).dataset.value"));
        await page.Keyboard.PressAsync("ArrowDown");   // Charlie is disabled: on to Delta
        Assert.Equal("d", await page.EvaluateAsync<string>("() => document.getElementById(document.getElementById('multi').getAttribute('aria-activedescendant')).dataset.value"));
        await page.Keyboard.PressAsync("Space");
        Assert.Equal(["b", "d"], await Values(page, "multi"));
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        Assert.Equal(["d", "e"], await Values(page, "multi"));
        await page.Keyboard.PressAsync(Keys.SelectAll);
        Assert.Equal(["a", "b", "d", "e"], await Values(page, "multi"));
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("e");           // typeahead
        Assert.Equal("e", await page.EvaluateAsync<string>("() => document.getElementById(document.getElementById('multi').getAttribute('aria-activedescendant')).dataset.value"));

        // One answer: the arrow keys choose as they go.
        await page.Locator("#single").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");   // starts on the first row, so the first step is the second
        Assert.Equal(["y"], await Values(page, "single"));
        await page.Keyboard.PressAsync("Home");
        Assert.Equal(["x"], await Values(page, "single"));
        await page.Keyboard.PressAsync("End");
        Assert.Equal(["z"], await Values(page, "single"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_change_sends_both_events_with_the_values()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await Js.Run(page, """
            () => {
                window.__seen = [];
                const box = document.getElementById('single');
                box.addEventListener('change', () => window.__seen.push('change'));
                document.addEventListener('sedna-listbox-select', e => window.__seen.push(e.detail.values.join(',')));
            }
            """);
        await page.Locator("#single [data-value=y]").ClickAsync();
        Assert.Equal(["change", "y"], await page.EvaluateAsync<string[]>("() => window.__seen"));
    }

    [Fact]
    public async Task A_chosen_row_is_the_brand_tint_with_a_tick_and_both_engines_agree()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture, "<style>*, *::before, *::after { transition: none !important; }</style>");
        var look = await page.EvaluateAsync<string[]>("""
            () => {
                const on = document.querySelector('#multi [data-value=b]'), off = document.querySelector('#multi [data-value=a]');
                const root = getComputedStyle(document.documentElement);
                const probe = document.createElement('i'); probe.style.background = 'var(--brand-tint)'; document.body.append(probe);
                const tint = getComputedStyle(probe).backgroundColor; probe.remove();
                const tick = getComputedStyle(on, '::after');
                return [getComputedStyle(on).backgroundColor === tint ? 'tint' : 'no tint: ' + getComputedStyle(on).backgroundColor,
                        getComputedStyle(off).backgroundColor === 'rgba(0, 0, 0, 0)' ? 'bare' : 'filled',
                        tick.content !== 'none' && tick.maskImage !== 'none' ? 'tick' : 'no tick'];
            }
            """);
        Assert.Equal(["tint", "bare", "tick"], look);
    }

    [Fact]
    public async Task A_transfer_moves_chosen_rows_between_two_listboxes()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await page.Locator("#from [role=option]:text('Kilo')").ClickAsync();
        await page.Locator("[data-transfer-add]").ClickAsync();
        Assert.Equal(["Hotel", "Kilo"], await Rows(page, "to"));
        Assert.Equal(["Gamma", "Lima"], await Rows(page, "from"));
        // Nothing arrives chosen, and a double-click moves one row.
        Assert.Equal(0, await page.Locator("#to [aria-selected=true]").CountAsync());
        await page.Locator("#from [role=option]:text('Lima')").DblClickAsync();
        Assert.Equal(["Hotel", "Kilo", "Lima"], await Rows(page, "to"));
        Assert.Empty(errors);
    }
}
