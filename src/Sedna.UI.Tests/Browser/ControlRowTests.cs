using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Controls that sit in a row with each other are the same height, measured in a
/// layout engine rather than derived from the stylesheet.
/// </summary>
/// <remarks>
/// <para>
/// This is the assertion that matters and the one a source scan cannot make. A
/// <c>min-height</c> is only a floor: whether it BINDS depends on the control's own
/// padding, its font size and its line box, and the moment the natural height exceeds
/// the token the height silently becomes a suggestion the control ignores.
/// </para>
/// <para>
/// Which is precisely what had happened. <c>.btn</c> was 33px, <c>.form-input</c> came
/// out at 35.5px from 8px of block padding, and a <c>.btn</c> inside an
/// <c>.input-group</c> stretched to a third height because the group added its own two
/// border pixels on top. Three numbers, no rule connecting them, and a form row where
/// the button beside the field was visibly short.
/// </para>
/// </remarks>
public class ControlRowTests : ScriptTestBase
{
    /// <summary>
    /// One of every control that can legitimately share a row, each in its own block.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT one flex row. Nine controls in a flex row overflow the fixture
    /// and every one of them shrinks below its own width, which measures the
    /// container rather than the control — a first draft of this test reported an
    /// icon button 13px wide and it was right about the box it had been given.
    /// </remarks>
    private const string Row =
        """
        <div style="padding:20px; width:640px">
          <p><button class="btn" id="btn">Reassign</button></p>
          <p><button class="btn btn-primary" id="btn-primary"><i class="ri-check-line"></i> Apply</button></p>
          <p><button class="btn btn-icon" id="btn-icon" aria-label="Refresh"><i class="ri-refresh-line"></i></button></p>
          <p><input class="form-input" id="input" value="ORD-4182" /></p>
          <p><select class="form-input form-select" id="select"><option>Any</option></select></p>
          <p><span class="form-value-display" id="value">EU-West</span></p>
          <p>
            <span class="input-group" id="group">
              <span class="input-affix"><i class="ri-search-line"></i></span>
              <input class="form-input" value="queue" />
              <button class="btn btn-icon" aria-label="Clear"><i class="ri-close-line"></i></button>
            </span>
          </p>
          <p><button class="btn btn-sm" id="sm">Export</button></p>
          <p><button class="btn btn-sm btn-icon" id="sm-icon" aria-label="Pin"><i class="ri-pushpin-line"></i></button></p>
          <p><input class="form-input form-input-sm" id="sm-input" value="queue" /></p>
          <p><span class="chip" id="sm-chip"><span class="chip-label">Priority 2</span></span></p>
          <p>
            <span class="input-group input-group--sm" id="sm-group">
              <input class="form-input" value="queue" />
              <button class="btn btn-icon" aria-label="Clear"><i class="ri-close-line"></i></button>
            </span>
          </p>
          <p><button class="btn btn-lg" id="lg">Continue</button></p>
          <p><button class="btn btn-lg btn-icon" id="lg-icon" aria-label="Next"><i class="ri-arrow-right-line"></i></button></p>
          <p><input class="form-input form-input-lg" id="lg-input" value="queue" /></p>
          <p><span class="chip chip-lg" id="lg-chip"><span class="chip-label">Priority 2</span></span></p>
          <p>
            <span class="input-group input-group--lg" id="lg-group">
              <input class="form-input" value="queue" />
              <button class="btn btn-icon" aria-label="Clear"><i class="ri-close-line"></i></button>
            </span>
          </p>
          <p>
            <span class="stepper" id="stepper">
              <button class="btn btn-icon" aria-label="Fewer">&minus;</button>
              <input class="form-input" type="number" value="3" />
              <button class="btn btn-icon" aria-label="More">+</button>
            </span>
          </p>
        </div>
        """;

    /// <summary>Every id in the fixture, in the order <see cref="Heights"/> returns.</summary>
    private static readonly string[] Ids =
    [
        "btn", "btn-primary", "btn-icon", "input", "select", "value", "group", "stepper",
        "sm", "sm-icon", "sm-input", "sm-chip", "sm-group",
        "lg", "lg-icon", "lg-input", "lg-chip", "lg-group",
    ];

    /// <summary>The members of each tier that must agree with each other.</summary>
    public static TheoryData<string, string[]> Tiers() => new()
    {
        { "small",  ["sm", "sm-icon", "sm-input", "sm-chip", "sm-group"] },
        { "normal", ["btn", "btn-primary", "btn-icon", "input", "select", "value", "group", "stepper", "lg-chip"] },
        { "large",  ["lg", "lg-icon", "lg-input", "lg-group"] },
    };

    /// <summary>
    /// The measured height of each control, by index into <see cref="Ids"/>.
    /// </summary>
    /// <remarks>
    /// An array rather than a map: Playwright's own serialisation is the only thing
    /// between the browser and here, and an ordered list of numbers is the shape it
    /// cannot get wrong.
    /// </remarks>
    private static async Task<Dictionary<string, double>> Heights(Microsoft.Playwright.IPage page)
    {
        var measured = await page.EvaluateAsync<double[]>(
            "ids => ids.map(id => Math.round("
            + "document.getElementById(id).getBoundingClientRect().height * 100) / 100)", Ids);

        return Ids.Zip(measured).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public async Task Every_control_in_a_tier_is_the_same_height(string tier, string[] members)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Row);

        var heights = await Heights(page);
        var distinct = members.Select(id => heights[id]).Distinct().ToList();

        Assert.True(distinct.Count == 1,
            $"The {tier} tier does not agree on a height, so a row of them steps up and down: "
            + string.Join(", ", members.Select(id => $"{id}={heights[id]}")));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_three_tiers_are_ordered()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Row);
        var heights = await Heights(page);

        Assert.True(heights["sm"] < heights["btn"], "small is not shorter than normal");
        Assert.True(heights["btn"] < heights["lg"], "normal is not shorter than large");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_control_height_token_is_the_height_that_is_used()
    {
        if (NoBrowser) return;
        // A min-height the content already exceeds is not a height. If this fails, some
        // control's own padding or line box has outgrown the token and the token has
        // quietly stopped deciding anything.
        var (page, errors) = await OpenStyled(Row);

        var measured = await page.EvaluateAsync<double[]>("""
            () => {
                const px = name => parseFloat(
                    getComputedStyle(document.documentElement).getPropertyValue(name));
                const h = id => document.getElementById(id).getBoundingClientRect().height;
                return [px('--control-height-sm'), px('--control-height'), px('--control-height-lg'),
                        h('sm'), h('sm-input'), h('btn'), h('input'), h('lg'), h('lg-input')];
            }
            """);

        Assert.Equal(measured[0], measured[3], 1);   // sm token vs .btn-sm
        Assert.Equal(measured[0], measured[4], 1);   // sm token vs .form-input-sm
        Assert.Equal(measured[1], measured[5], 1);   // normal token vs .btn
        Assert.Equal(measured[1], measured[6], 1);   // normal token vs .form-input
        Assert.Equal(measured[2], measured[7], 1);   // lg token vs .btn-lg
        Assert.Equal(measured[2], measured[8], 1);   // lg token vs .form-input-lg

        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_icon_only_button_is_square()
    {
        if (NoBrowser) return;
        // .btn-icon takes its width from the same token as its height, so "square" is
        // by construction rather than by two numbers that happen to agree.
        var (page, errors) = await OpenStyled(Row);

        foreach (var id in (string[])["btn-icon", "sm-icon", "lg-icon"])
        {
            var box = await page.EvaluateAsync<double[]>(
                $"() => {{ const r = document.getElementById('{id}').getBoundingClientRect(); "
                + "return [r.width, r.height]; }");

            Assert.Equal(box[1], box[0], 1);
        }

        Assert.Empty(errors);
    }
}
