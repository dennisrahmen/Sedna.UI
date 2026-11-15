using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// A corner count marks the corner of what it sits on: it follows the host's size, so it never
/// covers the icon or the avatar, and a ribbon steps through the labelling sizes.
/// </summary>
public class CornerMarkSizeTests : ScriptTestBase
{
    private const string Hosts = """
        <div style="display:flex;gap:24px;padding:24px;align-items:center">
          <button class="btn btn--sm btn--icon count-host" id="btn-sm" type="button" aria-label="a"><i class="ri-filter-3-line"></i><span class="count-badge">2</span></button>
          <button class="btn btn--icon count-host" id="btn" type="button" aria-label="a"><i class="ri-message-2-line"></i><span class="count-badge">5</span></button>
          <button class="btn btn--lg btn--icon count-host" id="btn-lg" type="button" aria-label="a"><i class="ri-message-2-line"></i><span class="count-badge">12</span></button>
          <span class="count-host count-host--round" id="av-sm"><span class="avatar avatar--sm">AF</span><span class="count-badge">4</span></span>
          <span class="count-host count-host--round" id="av"><span class="avatar">PN</span><span class="count-badge">4</span></span>
          <span class="count-host count-host--round" id="av-lg"><span class="avatar avatar--lg">JW</span><span class="count-badge">17</span></span>
          <span class="count-host" id="own" style="--count-size: 20px"><i class="ri-mail-line ri-2x"></i><span class="count-badge">3</span></span>
          <span class="count-host" id="named"><span class="avatar avatar--lg">JW</span><span class="count-badge count-badge--sm">3</span></span>
        </div>
        """;

    private static readonly string[] Ids = ["btn-sm", "btn", "btn-lg", "av-sm", "av", "av-lg"];

    private async Task<IPage> Open()
    {
        var (page, _) = await OpenStyled(Hosts);
        return page;
    }

    /// <summary>The host's smaller side, the number's height and the dot's, in pixels.</summary>
    private static Task<double[]> Sizes(IPage page, string id) => page.EvaluateAsync<double[]>($$"""
        () => {
            const host = document.getElementById('{{id}}');
            const badge = host.querySelector('.count-badge');
            const h = host.getBoundingClientRect();
            const n = badge.getBoundingClientRect();
            badge.textContent = '';
            const d = badge.getBoundingClientRect();
            return [Math.min(h.width, h.height), n.height, d.height];
        }
        """);

    [Fact]
    public async Task A_count_stays_under_the_share_of_its_host_that_marks_a_corner()
    {
        if (NoBrowser) return;
        var page = await Open();
        var failures = new List<string>();
        foreach (var id in Ids)
        {
            var s = await Sizes(page, id);
            // A number may take just over half the smallest host, a dot well under a third.
            if (s[1] > s[0] * 0.56) failures.Add($"#{id}: a number {s[1]}px on a {s[0]}px host");
            if (s[2] > s[0] * 0.31) failures.Add($"#{id}: a dot {s[2]}px on a {s[0]}px host");
        }
        Assert.True(failures.Count == 0, "These counts cover their host:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public async Task A_count_steps_with_the_size_of_its_host()
    {
        if (NoBrowser) return;
        var page = await Open();
        foreach (var family in new[] { new[] { "btn-sm", "btn", "btn-lg" }, new[] { "av-sm", "av", "av-lg" } })
        {
            var sizes = new List<double[]>();
            foreach (var id in family) sizes.Add(await Sizes(page, id));
            Assert.True(sizes[0][1] < sizes[1][1] && sizes[1][1] < sizes[2][1],
                $"The number does not grow with its host: {string.Join(", ", sizes.Select(s => s[1]))}.");
            Assert.True(sizes[0][2] < sizes[1][2] && sizes[1][2] < sizes[2][2],
                $"The dot does not grow with its host: {string.Join(", ", sizes.Select(s => s[2]))}.");
        }
    }

    [Fact]
    public async Task A_size_named_on_the_badge_or_the_token_on_the_host_wins_over_the_host()
    {
        if (NoBrowser) return;
        var page = await Open();
        Assert.Equal(20, (await Sizes(page, "own"))[1]);
        Assert.Equal(12, (await Sizes(page, "named"))[1]);
    }

    [Theory]
    [InlineData("ribbon ribbon--sm", "band")]
    [InlineData("ribbon ribbon--tab ribbon--sm", "tab")]
    public async Task A_ribbon_steps_through_the_labelling_sizes(string small, string shape)
    {
        if (NoBrowser) return;
        var large = small.Replace("--sm", "--lg", StringComparison.Ordinal);
        var plain = small.Replace(" ribbon--sm", "", StringComparison.Ordinal);
        var (page, _) = await OpenStyled($"""
            <div style="display:flex;gap:24px;padding:24px">
              <section class="card ribbon-host" style="width:240px;height:120px"><span class="{small}" id="s">New</span></section>
              <section class="card ribbon-host" style="width:240px;height:120px"><span class="{plain}" id="m">New</span></section>
              <section class="card ribbon-host" style="width:240px;height:120px"><span class="{large}" id="l">New</span></section>
            </div>
            """);
        var thickness = await page.EvaluateAsync<double[]>("""
            () => ['s', 'm', 'l'].map(id => document.getElementById(id).offsetHeight)
            """);
        Assert.True(thickness[0] < thickness[1] && thickness[1] < thickness[2],
            $"A {shape} ribbon does not grow through --sm, the default and --lg: {string.Join(", ", thickness)}.");
    }
}
