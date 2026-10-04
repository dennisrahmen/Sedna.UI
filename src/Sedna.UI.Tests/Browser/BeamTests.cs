using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.beam</c>: an edge in both of the agent's colours on the element's own border box,
/// held still, so a busy element costs nothing per frame.
/// </summary>
public class BeamTests : ScriptTestBase
{
    private const string Busy = """
        <div class="card beam" id="card" style="width: 320px; height: 120px">Summarising</div>
        <input class="form-input beam beam--glow" id="field" value="Filling in" />
        <button class="btn beam" id="button" type="button">Drafting</button>
        <span id="probe"></span>
        """;

    private static string Style(string id, string property) =>
        $"() => getComputedStyle(document.getElementById('{id}')).{property}";

    private async Task<IPage> OpenBusy(ReducedMotion motion = ReducedMotion.NoPreference) =>
        await Open(Busy, head: StylesheetTag, reducedMotion: motion);

    [Theory]
    [InlineData("card")]
    [InlineData("field")]
    [InlineData("button")]
    public async Task A_busy_element_carries_both_colours_round_its_edge_and_holds_still(string id)
    {
        if (NoBrowser) return;
        var page = await OpenBusy();
        var colours = await page.EvaluateAsync<string[]>("""
            () => ['--agent-from', '--agent-to'].map(token => {
                const p = document.getElementById('probe');
                p.style.color = `var(${token})`;
                return getComputedStyle(p).color;
            })
            """);

        var image = await page.EvaluateAsync<string>(Style(id, "backgroundImage"));
        Assert.Contains($"linear-gradient(90deg, {colours[0]}, {colours[1]})", image, StringComparison.Ordinal);
        Assert.Equal("padding-box, border-box", await page.EvaluateAsync<string>(Style(id, "backgroundClip")));
        Assert.Equal("rgba(0, 0, 0, 0)", await page.EvaluateAsync<string>(Style(id, "borderTopColor")));
        // Nothing moves: no frame of it is ever restyled or repainted.
        Assert.Equal("none", await page.EvaluateAsync<string>(Style(id, "animationName")));
    }

    [Fact]
    public async Task The_fill_under_the_edge_is_the_element_s_own()
    {
        if (NoBrowser) return;
        var page = await OpenBusy();
        // The fill is the first layer, over the padding box: the field keeps a field's.
        var fill = await page.EvaluateAsync<string>("""
            () => {
                const p = document.getElementById('probe');
                p.style.color = 'var(--bg)';
                return getComputedStyle(p).color;
            }
            """);
        Assert.StartsWith($"linear-gradient({fill}, {fill})", await page.EvaluateAsync<string>(Style("field", "backgroundImage")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_glow_holds_still_and_keeps_the_focus_ring()
    {
        if (NoBrowser) return;
        var page = await OpenBusy();
        var resting = await page.EvaluateAsync<string>(Style("field", "boxShadow"));
        Assert.NotEqual("none", resting);

        // The field eases its shadow in, so the ring is waited for rather than read at once.
        await page.FocusAsync("#field");
        await page.WaitForFunctionAsync($"() => ({Style("field", "boxShadow")})().includes('0px 0px 0px 2px')");
        Assert.Contains("0px 0px 18px -4px", await page.EvaluateAsync<string>(Style("field", "boxShadow")), StringComparison.Ordinal);
        Assert.Equal("none", await page.EvaluateAsync<string>(Style("field", "animationName")));
    }

    [Fact]
    public async Task Reduced_motion_draws_it_exactly_the_same()
    {
        if (NoBrowser) return;
        var still = await OpenBusy(ReducedMotion.Reduce);
        var stillImage = await still.EvaluateAsync<string>(Style("card", "backgroundImage"));
        var moving = await OpenBusy();
        Assert.Equal(stillImage, await moving.EvaluateAsync<string>(Style("card", "backgroundImage")));
    }
}
