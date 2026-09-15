using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Bars and meters measured: every row of a bar list draws its bar on one track length
/// whatever its value's width, and two meter marks close together keep their labels apart.
/// </summary>
public class DataVizLayoutTests : ScriptTestBase
{
    [Fact]
    public async Task Every_bar_list_row_has_the_same_track_length_whatever_its_value()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="bar-list" style="width:480px">
              <div class="bar-list-row">
                <span class="bar-list-label">EU-Central</span>
                <span class="bar-list-track"><span class="bar-list-fill" style="--value: 100%"></span></span>
                <span class="bar-list-value">1 240 000</span>
              </div>
              <div class="bar-list-row">
                <span class="bar-list-label">EU-West</span>
                <span class="bar-list-track"><span class="bar-list-fill" style="--value: 40%"></span></span>
                <span class="bar-list-value">12</span>
              </div>
            </div>
            """);

        var widths = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('.bar-list-track')].map(t => t.getBoundingClientRect().width)
            """);

        Assert.True(widths[0] > 100, $"The track is {widths[0]}px wide.");
        Assert.Equal(widths[0], widths[1], 0.01);
    }

    [Fact]
    public async Task A_mark_label_moved_above_the_meter_does_not_overlap_the_one_below()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:320px; padding:40px 0">
              <div id="meter" class="meter meter--labelled">
                <span class="meter-fill" style="--value: 71%"></span>
                <span class="meter-mark" style="--at: 80%"><span class="meter-mark-label">alert 80%</span></span>
                <span class="meter-mark meter-mark--above" style="--at: 90%"><span class="meter-mark-label">critical 90%</span></span>
              </div>
            </div>
            """);

        var boxes = await page.EvaluateAsync<double[]>("""
            () => {
                const m = document.getElementById('meter').getBoundingClientRect();
                const [below, above] = [...document.querySelectorAll('.meter-mark-label')].map(l => l.getBoundingClientRect());
                return [below.top, m.bottom, above.bottom, m.top, parseFloat(getComputedStyle(document.getElementById('meter')).marginTop)];
            }
            """);

        Assert.True(boxes[0] >= boxes[1], "The first label sits under the track.");
        Assert.True(boxes[2] <= boxes[3], "The moved label sits over the track.");
        Assert.True(boxes[4] > 0, "--labelled reserves the line above once a mark asks for it.");
    }
}
