using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Files, video and audio: a name cut in the middle, a name column that does not widen its
/// table, tiles that select from their checkbox, and players that hold their shape.
/// </summary>
public class FileLayoutTests : ScriptTestBase
{
    private const string LongName = """
        <span class="file-name" id="name" style="width:180px">
            <span class="file-name-start" id="start">delivery-note-week-38-carrier-return-</span><span class="file-name-end" id="end">signed.pdf</span>
        </span>
        """;

    [Fact]
    public async Task A_long_name_is_cut_in_the_middle_and_keeps_its_end()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(LongName);

        var name = await page.Locator("#name").BoundingBoxAsync();
        var end = await page.Locator("#end").BoundingBoxAsync();
        Assert.True(end!.X + end.Width <= name!.X + name.Width + 0.5,
            "The end of the name is clipped; only the start may be cut.");
        Assert.True(await page.EvaluateAsync<bool>("() => { const s = document.getElementById('start'); return s.scrollWidth > s.clientWidth; }"),
            "The start of a name wider than its box should overflow into the ellipsis.");
    }

    [Fact]
    public async Task A_name_column_does_not_push_its_table_wider_than_its_card()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="card" id="card" style="width:560px">
                <table class="table" id="table">
                    <tbody><tr>
                        <td><span class="file-cell"><span class="file-icon file-icon--sm"><i></i></span><span class="file-name"><span class="file-name-start">delivery-note-week-38-carrier-return-and-the-second-and-the-third-copy-for-the-archive-</span><span class="file-name-end" id="end">signed.pdf</span></span></span></td>
                        <td class="col-fit">12.8 MB</td>
                        <td class="col-fit">Yesterday</td>
                        <td class="col-fit">Alex Fischer</td>
                    </tr></tbody>
                </table>
            </div>
            """);

        var card = await page.Locator("#card").BoundingBoxAsync();
        var table = await page.Locator("#table").BoundingBoxAsync();
        Assert.True(table!.Width <= card!.Width + 0.5, $"The table is {table.Width}px in a {card.Width}px card.");
        var end = await page.Locator("#end").BoundingBoxAsync();
        Assert.True(end!.X + end.Width <= card.X + card.Width, "The end of the name was pushed out of the card.");
    }

    private const string Grid = """
        <ul class="file-grid" id="grid" style="width:300px">
            <li class="file-tile" id="t1">
                <label class="form-check file-tile-check" id="c1"><input type="checkbox" id="i1" aria-label="Select one" /></label>
                <a class="file-tile-open" id="o1" href="#"><span class="file-tile-thumb"></span><span class="file-name">one.pdf</span></a>
            </li>
            <li class="file-tile" id="t2">
                <label class="form-check file-tile-check" id="c2"><input type="checkbox" aria-label="Select two" /></label>
                <a class="file-tile-open" href="#"><span class="file-tile-thumb"></span><span class="file-name">two.pdf</span></a>
            </li>
        </ul>
        """;

    [Fact]
    public async Task A_phone_width_grid_holds_two_tiles_a_row()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);

        var one = await page.Locator("#t1").BoundingBoxAsync();
        var two = await page.Locator("#t2").BoundingBoxAsync();
        Assert.True(Math.Abs(one!.Y - two!.Y) < 1, "Two tiles in a 300px grid should share a row.");
    }

    [Fact]
    public async Task Checking_a_tile_selects_it_and_shows_every_checkbox()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);

        async Task<double> Opacity(string id) =>
            await page.EvaluateAsync<double>($"() => parseFloat(getComputedStyle(document.getElementById('{id}')).opacity)");
        async Task<string> Border(string id) =>
            await page.EvaluateAsync<string>($"() => getComputedStyle(document.getElementById('{id}')).borderTopColor");

        await page.Mouse.MoveAsync(0, 400);
        Assert.Equal(0, await Opacity("c2"));
        var unselected = await Border("t1");

        await page.Locator("#i1").CheckAsync(new() { Force = true });
        await page.Mouse.MoveAsync(0, 400);
        await page.WaitForTimeoutAsync(250);

        Assert.NotEqual(unselected, await Border("t1"));
        Assert.Equal(1, await Opacity("c2"));
    }

    // A 16:9 and a 2:3 picture, sized by their attributes as a real thumbnail is.
    private const string Wide = """<img src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='320' height='180'/%3E" width="320" height="180" alt="">""";
    private const string Tall = """<img src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='300'/%3E" width="200" height="300" alt="">""";

    [Fact]
    public async Task A_contained_thumb_keeps_its_frame_and_a_natural_one_takes_the_pictures_shape()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <ul class="file-grid" style="width:200px;grid-template-columns:1fr">
              <li class="file-tile"><span class="file-tile-thumb file-tile-thumb--contain" id="contain">{Wide}</span></li>
              <li class="file-tile"><span class="file-tile-thumb file-tile-thumb--natural" id="natural">{Wide}</span></li>
              <li class="file-tile"><span class="file-tile-thumb file-tile-thumb--natural" id="tall">{Tall}</span></li>
              <li class="file-tile"><span class="file-tile-thumb file-tile-thumb--natural" id="icon"><span class="file-icon"><i></i></span></span></li>
            </ul>
            <ul class="file-grid file-grid--wide" style="width:300px">
              <li class="file-tile"><span class="file-tile-thumb file-tile-thumb--natural" id="wide-grid">{Tall}</span></li>
            </ul>
            """);

        async Task<double> Ratio(string id) => await page.EvaluateAsync<double>(
            $"() => {{ const r = document.getElementById('{id}').getBoundingClientRect(); return r.width / r.height; }}");

        Assert.Equal(1, await Ratio("contain"), 2);
        Assert.Equal("contain", await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('#contain > img')).objectFit"));
        Assert.Equal(320.0 / 180, await Ratio("natural"), 2);
        Assert.Equal(200.0 / 300, await Ratio("tall"), 2);
        Assert.Equal(1, await Ratio("icon"), 2);              // no picture: the frame stays
        Assert.Equal(200.0 / 300, await Ratio("wide-grid"), 2); // --wide's 16:9 does not win
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_wrapped_name_shows_whole_inside_a_narrow_tile()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <ul class="file-grid" style="width:140px;grid-template-columns:1fr">
              <li class="file-tile" id="tile">
                <a class="file-tile-open" href="#"><span class="file-tile-thumb"></span>
                  <span class="file-name file-name--wrap" id="name">IMG_20260918_102915_warehouse_open_day_group_photo.jpeg</span></a>
              </li>
            </ul>
            """);

        var probe = await page.EvaluateAsync<double[]>("""
            () => {
                const tile = document.getElementById('tile').getBoundingClientRect();
                const name = document.getElementById('name');
                const r = name.getBoundingClientRect();
                const line = parseFloat(getComputedStyle(name).lineHeight);
                return [tile.right - r.right, name.scrollWidth - name.clientWidth, r.height / line];
            }
            """);

        Assert.True(probe[0] >= 0, "The name runs out of its tile.");
        Assert.Equal(0, probe[1], 0);                     // nothing clipped
        Assert.True(probe[2] > 1.5, "A long name should wrap onto more lines.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_always_visible_tile_action_needs_no_hover()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <ul class="file-grid" style="width:300px">
              <li class="file-tile">
                <label class="form-check file-tile-check" id="check"><input type="checkbox" aria-label="Select one" /></label>
                <a class="file-tile-open" href="#"><span class="file-tile-thumb"></span><span class="file-name">one.pdf</span></a>
                <button class="btn btn-icon file-tile-menu file-tile-menu--always" id="always" type="button" aria-label="Remove one.pdf"><i></i></button>
                <button class="btn btn-icon file-tile-menu" id="plain" type="button" aria-label="Actions for one.pdf" style="top:auto;bottom:0"><i></i></button>
              </li>
            </ul>
            """);
        await page.Mouse.MoveAsync(0, 600);

        async Task<double> Opacity(string id) =>
            await page.EvaluateAsync<double>($"() => parseFloat(getComputedStyle(document.getElementById('{id}')).opacity)");

        Assert.Equal(1, await Opacity("always"));
        Assert.Equal(0, await Opacity("plain"));   // the modifier, not a change to every menu
        Assert.Equal(0, await Opacity("check"));   // and not to the checkbox
    }

    [Fact]
    public async Task The_whole_tile_opens_the_file()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);

        // The thumbnail's middle is the link's stretched hit area, not the checkbox or the menu.
        var tile = await page.Locator("#t1").BoundingBoxAsync();
        var hit = await page.EvaluateAsync<string>(
            $"() => document.elementFromPoint({tile!.X + tile.Width / 2}, {tile.Y + tile.Height / 3}).closest('a, label')?.id ?? ''");
        Assert.Equal("o1", hit);
    }

    [Fact]
    public async Task A_video_holds_its_ratio_before_it_loads()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <figure class="media-player" style="width:640px"><video id="v" controls preload="none"></video></figure>
            """);

        var box = await page.Locator("#v").BoundingBoxAsync();
        Assert.True(Math.Abs(box!.Height - box.Width * 9 / 16) < 2, $"The video is {box.Width}×{box.Height}, not 16:9.");
    }

    [Fact]
    public async Task A_waveform_keeps_its_time_on_one_line_when_narrow()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:170px">
                <div class="audio-wave">
                    <button class="btn btn-sm btn-icon" type="button" aria-label="Play">P</button>
                    <span class="audio-wave-bars"><i></i><i></i><i></i><i></i></span>
                    <span class="audio-wave-time" id="time">0:07 / 0:19</span>
                </div>
            </div>
            """);

        var height = await page.EvaluateAsync<double>("() => document.getElementById('time').getBoundingClientRect().height");
        var line = await page.EvaluateAsync<double>("() => { const s = getComputedStyle(document.getElementById('time')); return parseFloat(s.fontSize) * 1.8; }");
        Assert.True(height < line, $"The time wrapped: {height}px tall.");
    }

    [Fact]
    public async Task A_waveform_is_drawn_the_same_in_a_wide_row_and_in_a_bubble_that_fits_its_content()
    {
        if (NoBrowser) return;
        const string wave = """
            <button class="btn btn-sm btn-icon" type="button" aria-label="Play">P</button>
            <span class="audio-wave-bars"><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i></span>
            <span class="audio-wave-time">0:07 / 0:19</span>
            """;
        var (page, _) = await OpenStyled($"""
            <div style="width:600px"><div class="audio-wave" id="wide">{wave}</div></div>
            <div style="display:inline-block; max-width:320px"><div class="audio-wave" id="fit">{wave}</div></div>
            """);

        var bars = await page.EvaluateAsync<double[]>("""
            () => ['wide', 'fit'].flatMap(id => {
                const b = document.querySelector('#' + id + ' .audio-wave-bars');
                return [b.getBoundingClientRect().width, b.firstElementChild.getBoundingClientRect().width];
            })
            """);

        Assert.Equal(bars[0], bars[2], 0.01);   // the same bars take the same width
        Assert.Equal(bars[1], bars[3], 0.01);   // and each bar is as thick in both
    }

    [Fact]
    public async Task A_track_number_and_the_playing_glyph_take_the_same_width()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <ol class="list" style="width:360px">
                <li><button class="list-row" type="button"><span class="media-track-num">1</span><span class="list-main" id="a"><span class="list-title">One</span></span></button></li>
                <li><button class="list-row" type="button" aria-current="true"><span class="media-track-num"><i style="display:inline-block;width:18px">E</i></span><span class="list-main" id="b"><span class="list-title">Two</span></span></button></li>
                <li><button class="list-row" type="button"><span class="media-track-num">12</span><span class="list-main" id="c"><span class="list-title">Twelve</span></span></button></li>
            </ol>
            """);

        var a = (await page.Locator("#a").BoundingBoxAsync())!.X;
        var b = (await page.Locator("#b").BoundingBoxAsync())!.X;
        var c = (await page.Locator("#c").BoundingBoxAsync())!.X;
        Assert.True(Math.Abs(a - b) < 0.5 && Math.Abs(a - c) < 0.5, $"The titles start at {a}, {b} and {c}.");
    }
}
