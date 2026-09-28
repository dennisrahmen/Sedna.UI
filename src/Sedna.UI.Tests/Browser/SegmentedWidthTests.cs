using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.segmented</c> is as wide as its options wherever it sits, except in a table's
/// filter row, which asks it to fill the cell.
/// </summary>
public class SegmentedWidthTests : ScriptTestBase
{
    private const string Control = """
        <label class="segmented-option"><input type="radio" name="{0}" checked> On</label>
        <label class="segmented-option"><input type="radio" name="{0}"> Off</label>
        """;

    // A column stretches its children, so under a .form-label an On/Off switch used to
    // run the full width of the field.
    [Theory]
    [InlineData("form-field")]
    [InlineData("form-section")]
    [InlineData("sedna-col")]
    public async Task A_column_does_not_stretch_it(string container)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div class="{container}" id="box" style="width:600px">
              <span class="form-label">Enabled</span>
              <div class="segmented" id="seg">{string.Format(Control, "a")}</div>
            </div>
            """);

        var widths = await page.EvaluateAsync<double[]>("""
            () => ['box', 'seg'].map(id => document.getElementById(id).getBoundingClientRect().width)
            """);

        Assert.True(widths[1] < 200, $"The segmented control is {widths[1]}px wide in a {widths[0]}px {container}.");
        Assert.Empty(errors);
    }

    // A kind switch at the top of a capture dialog, whose body is a column: the shape
    // several consuming apps wrote, and the one that made the bug look universal.
    [Fact]
    public async Task A_modal_body_that_is_a_column_does_not_stretch_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <dialog class="modal" id="dlg">
              <div class="modal-header"><h3>Capture</h3></div>
              <div class="modal-body sedna-col sedna-gap-2" id="box">
                <div class="segmented" id="seg" role="group" aria-label="Kind">{string.Format(Control, "c")}</div>
                <div class="form-field"><label class="form-label" for="t">Title</label><input class="form-input" id="t"></div>
              </div>
            </dialog>
            """);
        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");

        var widths = await page.EvaluateAsync<double[]>("""
            () => ['box', 'seg', 't'].map(id => document.getElementById(id).getBoundingClientRect().width)
            """);

        Assert.True(widths[1] < widths[2] / 2,
            $"The segmented control is {widths[1]}px wide beside a {widths[2]}px field in a {widths[0]}px body.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_filter_row_cell_still_fills()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <table class="table" style="width:600px">
              <thead>
                <tr><th>Name</th><th>Monitored</th></tr>
                <tr class="tr-filter"><th></th><th id="cell"><div class="segmented segmented--sm" id="seg">{string.Format(Control, "b")}</div></th></tr>
              </thead>
            </table>
            """);

        var widths = await page.EvaluateAsync<double[]>("""
            () => {
              const cell = document.getElementById('cell');
              const s = getComputedStyle(cell);
              return [cell.clientWidth - parseFloat(s.paddingLeft) - parseFloat(s.paddingRight),
                      document.getElementById('seg').getBoundingClientRect().width];
            }
            """);

        Assert.Equal(widths[0], widths[1], 1.0);
    }
}
