using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-clamp-toggle</c>: the app's "Show more" button opens a clamped block in full and
/// closes it again, its own two labels follow <c>aria-expanded</c>, and it is not shown at all
/// where the text fits inside the clamp — measured again when the block changes size.
/// </summary>
public class ClampTests : ScriptTestBase
{
    private const string Long =
        "The customer asked for the two backordered lines to follow in one parcel rather than separately, "
        + "and for the invoice to name the warehouse they ship from. Hold the parcel until both lines are in, "
        + "then send it with the delivery note. If either line slips past Friday, call them before shipping.";

    private static string Fixture => $"""
        <div id="box" style="width:300px">
          <p class="text-clamp-2" id="long">{Long}</p>
          <button class="btn-bare btn-bare--link" type="button" data-clamp-toggle aria-controls="long" aria-expanded="false" id="more">
            <span class="text-clamp-more" id="more-label">Show more</span>
            <span class="text-clamp-less" id="less-label">Show less</span>
          </button>
          <p class="text-clamp-2" id="short">Leave it with the neighbour.</p>
          <button class="btn-bare btn-bare--link" type="button" data-clamp-toggle aria-controls="short" aria-expanded="false" id="short-more">
            <span class="text-clamp-more">Show more</span>
            <span class="text-clamp-less">Show less</span>
          </button>
          <p class="text-clamp-2" id="sibling">{Long}</p>
          <button class="btn-bare btn-bare--link" type="button" data-clamp-toggle aria-expanded="false" id="sibling-more">
            <span class="text-clamp-more">Show more</span>
            <span class="text-clamp-less">Show less</span>
          </button>
        </div>
        """;

    private static Task<double> Height(Microsoft.Playwright.IPage page, string id) =>
        page.EvaluateAsync<double>("id => document.getElementById(id).getBoundingClientRect().height", id);

    private static Task<string> Display(Microsoft.Playwright.IPage page, string id) =>
        page.EvaluateAsync<string>("id => getComputedStyle(document.getElementById(id)).display", id);

    [Fact]
    public async Task Show_more_opens_the_block_and_show_less_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var closed = await Height(page, "long");
        Assert.Equal("none", await Display(page, "less-label"));
        Assert.NotEqual("none", await Display(page, "more-label"));

        await page.ClickAsync("#more");
        Assert.Equal("true", await page.GetAttributeAsync("#more", "aria-expanded"));
        Assert.NotNull(await page.GetAttributeAsync("#long", "data-clamp-open"));
        Assert.True(await Height(page, "long") > closed * 1.5, "The block did not open past its clamp.");
        Assert.Equal("none", await Display(page, "more-label"));
        Assert.NotEqual("none", await Display(page, "less-label"));

        await page.ClickAsync("#more");
        Assert.Equal("false", await page.GetAttributeAsync("#more", "aria-expanded"));
        Assert.Null(await page.GetAttributeAsync("#long", "data-clamp-open"));
        Assert.Equal(closed, await Height(page, "long"), 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_button_is_not_shown_where_the_text_fits()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);

        await page.WaitForFunctionAsync("() => document.getElementById('short-more').hasAttribute('data-clamp-fits')");
        Assert.Equal("none", await Display(page, "short-more"));
        Assert.Null(await page.GetAttributeAsync("#more", "data-clamp-fits"));
        Assert.NotEqual("none", await Display(page, "more"));
    }

    [Fact]
    public async Task A_narrower_block_that_now_overflows_shows_its_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await page.WaitForFunctionAsync("() => document.getElementById('short-more').hasAttribute('data-clamp-fits')");

        await Js.Run(page, "() => { document.getElementById('box').style.width = '60px'; }");
        await page.WaitForFunctionAsync("() => !document.getElementById('short-more').hasAttribute('data-clamp-fits')");
        Assert.NotEqual("none", await Display(page, "short-more"));
    }

    [Fact]
    public async Task Without_aria_controls_the_block_is_the_one_before_the_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);

        await page.ClickAsync("#sibling-more");
        Assert.NotNull(await page.GetAttributeAsync("#sibling", "data-clamp-open"));
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.clamp.toggle('sibling-more') === false"));
        Assert.Null(await page.GetAttributeAsync("#sibling", "data-clamp-open"));
    }
}
