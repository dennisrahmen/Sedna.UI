using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>--fill</c> makes a control as wide as its container, and a row of options share
/// the width equally while every word fits; a tab too narrow for its longest word grows rather
/// than break it.
/// </summary>
public class FillModifierTests : ScriptTestBase
{
    [Fact]
    public async Task A_filling_button_is_as_wide_as_its_block_container()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width: 320px" id="box"><button class="btn btn--fill" type="button" id="b">Choose</button></div>
            """);

        var widths = await page.EvaluateAsync<double[]>(
            "() => [document.getElementById('b').getBoundingClientRect().width, document.getElementById('box').getBoundingClientRect().width]");
        Assert.True(Math.Abs(widths[0] - widths[1]) < 0.5, $"The button is {widths[0]:0}px wide in a {widths[1]:0}px container.");
    }

    public static TheoryData<string, string> Rows() => new()
    {
        { "tabs tabs--fill", "<button class=\"tab\" type=\"button\">Overview</button><button class=\"tab\" type=\"button\">Sign-in</button><button class=\"tab\" type=\"button\">Computers</button><button class=\"tab\" type=\"button\">Judge tablets and their owners</button>" },
        { "segmented segmented--fill", "<label class=\"segmented-option\"><input type=\"radio\" name=\"r\" checked> <span>Standard</span></label><label class=\"segmented-option\"><input type=\"radio\" name=\"r\"> <span>Express</span></label><label class=\"segmented-option\"><input type=\"radio\" name=\"r\"> <span>Pick-up at the counter</span></label>" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task A_filling_row_shares_the_width_equally_and_never_overflows(string classes, string items)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width: 480px"><div class="{classes}" id="row">{items}</div></div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const row = document.getElementById('row');
                const widths = [...row.children].map(c => c.getBoundingClientRect().width);
                return [Math.max(...widths) - Math.min(...widths), row.scrollWidth - row.clientWidth,
                        row.getBoundingClientRect().width, row.parentElement.getBoundingClientRect().width];
            }
            """);
        Assert.True(r[0] < 1, $"The options of .{classes} differ in width by {r[0]:0.0}px.");
        Assert.True(r[1] <= 0, $".{classes} overflows by {r[1]}px.");
        Assert.True(Math.Abs(r[2] - r[3]) < 0.5, $".{classes} is {r[2]:0}px wide in a {r[3]:0}px container.");
    }

    public static TheoryData<string, string> RowsWithAMenu() => new()
    {
        { "tabs tabs--fill", "<a class=\"tab tab--active\" href=\"#\">Live</a><a class=\"tab\" href=\"#\">Ads</a><a class=\"tab\" href=\"#\">Designer</a><div class=\"menu-anchor\"><button class=\"tab\" type=\"button\" aria-haspopup=\"true\" id=\"trigger\"><span>Setup</span> <i class=\"ri-arrow-down-s-line\"></i></button><div class=\"menu\" hidden><button class=\"menu-item\" type=\"button\">Devices</button></div></div>" },
        { "segmented segmented--fill", "<label class=\"segmented-option\"><input type=\"radio\" name=\"r\" checked> <span>Day</span></label><label class=\"segmented-option\"><input type=\"radio\" name=\"r\"> <span>Week</span></label><div class=\"menu-anchor\"><button class=\"segmented-option\" type=\"button\" aria-haspopup=\"true\" id=\"trigger\"><span>More</span></button><div class=\"menu\" hidden><button class=\"menu-item\" type=\"button\">Year</button></div></div>" },
    };

    /// <summary>
    /// Every filling row treats an item in a <c>.menu-anchor</c> as an item: the anchor takes an
    /// equal share and the trigger fills it. A new filling row gets a line here.
    /// </summary>
    [Theory]
    [MemberData(nameof(RowsWithAMenu))]
    public async Task An_item_that_opens_a_menu_takes_its_share_of_a_filling_row(string classes, string items)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width: 480px"><div class="{classes}" id="row">{items}</div></div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const row = document.getElementById('row');
                const widths = [...row.children].map(c => c.getBoundingClientRect().width);
                const t = document.getElementById('trigger').getBoundingClientRect(), a = document.getElementById('trigger').parentElement.getBoundingClientRect();
                const sibling = row.firstElementChild.getBoundingClientRect();
                return [Math.max(...widths) - Math.min(...widths), t.width - a.width, Math.abs(t.top - sibling.top) + Math.abs(t.bottom - sibling.bottom), row.scrollWidth - row.clientWidth];
            }
            """);
        Assert.True(r[0] < 1, $"The items of .{classes} differ in width by {r[0]:0.0}px with one in a menu anchor.");
        Assert.True(Math.Abs(r[1]) < 0.5, $"The trigger is {r[1]:0.0}px off its anchor's width.");
        Assert.True(r[2] < 0.5, $"The trigger's top and bottom are {r[2]:0.0}px off its siblings'.");
        Assert.True(r[3] <= 0, $".{classes} overflows by {r[3]}px.");
    }

    [Fact]
    public async Task A_filling_tab_too_narrow_for_its_word_grows_rather_than_break_it()
    {
        if (NoBrowser) return;
        // Four tabs in 360px: a quarter (90px) is narrower than "Computers" (about 101px), which
        // broke mid-word when the shares were held equal. Every word fits the row, so the
        // row still fits its container.
        var (page, _) = await OpenStyled("""
            <div style="width: 360px"><div class="tabs tabs--fill" id="row"><button class="tab" type="button">Overview</button><button class="tab" type="button">Sign-in</button><button class="tab" type="button" id="w">Computers</button><button class="tab" type="button">Judge tablets and their owners</button></div></div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const row = document.getElementById('row');
                const tab = document.getElementById('w');
                const range = document.createRange();
                range.selectNodeContents(tab);
                return [range.getClientRects().length, row.scrollWidth - row.clientWidth];
            }
            """);
        Assert.True(r[0] < 1.5, $"\"Computers\" is broken over {r[0]} lines.");
        Assert.True(r[1] <= 0, $"The row overflows by {r[1]}px.");
    }
}
