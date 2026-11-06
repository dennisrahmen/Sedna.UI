using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// aria-busy on a control and a surface, and aria-readonly on choice controls, as a reader
/// meets them: what they show, and what the pointer and the keyboard can still do.
/// </summary>
public class BusyReadonlyTests : ScriptTestBase
{
    [Fact]
    public async Task A_busy_control_turns_its_icon_into_the_spinner_and_a_busy_surface_dims_under_a_bar()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="menu" style="position:static"><button class="menu-item" id="item" aria-busy="true"><i class="ri-refresh-line"></i> Refresh</button></div>
            <div class="card" id="card" aria-busy="true"><div class="card-body" id="body">Rows</div></div>
            """);

        var item = await page.EvaluateAsync<string[]>("""
            () => { const i = document.querySelector('#item > i'); const s = getComputedStyle(i, '::after');
                    return [s.content, s.borderTopStyle, getComputedStyle(document.getElementById('item')).pointerEvents, getComputedStyle(i).color]; }
            """);
        Assert.Equal("\"\"", item[0]);
        Assert.Equal("solid", item[1]);
        Assert.Equal("none", item[2]);
        Assert.Equal("rgba(0, 0, 0, 0)", item[3]);

        var card = await page.EvaluateAsync<string[]>("""
            () => [getComputedStyle(document.getElementById('card')).backgroundImage, getComputedStyle(document.getElementById('body')).opacity]
            """);
        Assert.StartsWith("linear-gradient", card[0], StringComparison.Ordinal);
        Assert.Equal("0.55", card[1]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_read_only_check_and_select_keep_their_value_under_the_keyboard()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <label class="form-check"><input type="checkbox" id="c" aria-readonly="true" checked> Notify the customer</label>
            <select class="form-select" id="s" aria-readonly="true"><option>Open</option><option>Shipped</option></select>
            """);

        await page.FocusAsync("#c");
        await page.Keyboard.PressAsync("Space");
        Assert.True(await page.IsCheckedAsync("#c"));

        await page.FocusAsync("#s");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("Open", await page.EvaluateAsync<string>("() => document.getElementById('s').value"));
        Assert.Empty(errors);
    }
}
