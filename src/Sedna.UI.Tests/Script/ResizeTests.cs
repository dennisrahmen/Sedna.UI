using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>[data-resize]</c>: a card's height and a split's aside move with a drag, snap to the
/// step, stop at their bounds, answer the keyboard, reset, and say where they ended.
/// </summary>
public class ResizeTests : ScriptTestBase
{
    private const string Card = """
        <div style="width:400px">
          <div class="card card--resizable" id="card">
            <div class="card-head"><h3>Open orders</h3></div>
            <div class="card-body"><p style="height:600px">Rows</p></div>
            <div class="card-resize" id="h" data-resize="--card-height" role="separator" aria-orientation="horizontal"
                 aria-label="Resize" aria-valuemin="120" aria-valuemax="400" tabindex="0"></div>
          </div>
        </div>
        <script>window.ended = []; document.addEventListener('sedna-resize', e => window.ended.push(e.detail.value));</script>
        """;

    private const string Split = """
        <div style="width:900px">
          <div class="sedna-split sedna-split--stack-sm" id="s">
            <div id="main">Main</div>
            <div class="split-resize" id="h" data-resize="--split-aside" data-resize-measure=".sedna-split-aside" role="separator"
                 aria-orientation="vertical" aria-label="Resize" aria-valuemin="160" aria-valuemax="480" tabindex="0"></div>
            <aside class="sedna-split-aside" id="aside">Aside</aside>
          </div>
        </div>
        """;

    private static Task<double> Height(IPage page, string id) =>
        page.EvaluateAsync<double>("id => document.getElementById(id).getBoundingClientRect().height", id);

    private static Task<double> Width(IPage page, string id) =>
        page.EvaluateAsync<double>("id => document.getElementById(id).getBoundingClientRect().width", id);

    [Fact]
    public async Task A_card_grows_by_a_drag_snapped_to_the_step_and_stops_at_its_maximum()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Card);
        await Js.Run(page, "() => document.getElementById('card').style.setProperty('--card-height', '200px')");
        var box = (await page.Locator("#h").BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 53, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        Assert.Equal(256, await Height(page, "card"), 0.5);           // 200 + 53, snapped to 8

        await page.Mouse.MoveAsync(x, y + 53);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 500, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        Assert.Equal(400, await Height(page, "card"), 0.5);           // aria-valuemax

        Assert.Equal("400", await page.GetAttributeAsync("#h", "aria-valuenow"));
        Assert.Equal(new[] { 256.0, 400.0 }, await page.EvaluateAsync<double[]>("() => window.ended"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_moves_it_a_step_and_Home_puts_it_back()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Card);
        await Js.Run(page, "() => document.getElementById('card').style.setProperty('--card-height', '200px')");
        await page.FocusAsync("#h");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal(208, await Height(page, "card"), 0.5);
        await page.Keyboard.PressAsync("Shift+ArrowUp");
        Assert.Equal(176, await Height(page, "card"), 0.5);

        await page.Keyboard.PressAsync("Home");
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('card').style.getPropertyValue('--card-height')"));
        Assert.True(await Height(page, "card") > 600, "Without a height the card is as tall as its content.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_split_aside_after_the_handle_grows_as_the_handle_moves_towards_the_main_column()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Split);
        var before = await Width(page, "aside");
        var box = (await page.Locator("#h").BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 64, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();

        Assert.Equal(Math.Round((before + 64) / 8) * 8, await Width(page, "aside"), 1.0);
        Assert.Empty(errors);
    }
}
