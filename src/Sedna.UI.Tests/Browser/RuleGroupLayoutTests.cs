using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The query builder's frame measured in a layout engine: a nested group indented from its
/// parent with the line on its inline-start edge in either direction, a condition row whose
/// four parts sit on one line in order, and a narrow group whose fields go one under the other
/// with the remove button beside the first — nothing spilling out of the group.
/// </summary>
public class RuleGroupLayoutTests : ScriptTestBase
{
    private const string Row = """
        <div class="rule-group-row">
          <select class="form-select form-select--sm" aria-label="Field"><option>Status</option></select>
          <select class="form-select form-select--sm" aria-label="Operator"><option>is not</option></select>
          <input class="form-input form-input--sm" aria-label="Value" value="Held" />
          <button class="btn btn--icon btn--sm btn--ghost" type="button" aria-label="Remove"><i class="ri-delete-bin-line"></i></button>
        </div>
        """;

    private static string Fixture(string dir, int width) => $"""
        <div dir="{dir}" style="width:{width}px">
          <div class="rule-group" id="root" role="group" aria-label="Conditions">
            <div class="rule-group-head">
              <span>Match</span>
              <div class="segmented segmented--sm" role="radiogroup" aria-label="Combine">
                <label class="segmented-option"><input type="radio" name="r" checked /> <span>AND</span></label>
                <label class="segmented-option"><input type="radio" name="r" /> <span>OR</span></label>
              </div>
            </div>
            <div class="rule-group-body" id="root-body">
              <div id="root-row">{Row}</div>
              <div class="rule-group" id="nested" role="group" aria-label="Nested">
                <div class="rule-group-head"><span>Match</span></div>
                <div class="rule-group-body" id="nested-body">
                  <div id="nested-row">{Row}</div>
                </div>
              </div>
              <div class="rule-group-actions">
                <button class="btn btn--sm btn--ghost" type="button"><i class="ri-add-line"></i> <span>Add condition</span></button>
              </div>
            </div>
          </div>
        </div>
        """;

    private const string Measure = """
        () => {
            const rect = el => { const r = el.getBoundingClientRect(); return { left: r.left, right: r.right, top: r.top, bottom: r.bottom }; };
            const parts = id => [...document.querySelector('#' + id + ' > .rule-group-row').children].map(rect);
            const body = id => { const cs = getComputedStyle(document.getElementById(id)); return { left: cs.borderLeftWidth, right: cs.borderRightWidth }; };
            return {
                root: rect(document.getElementById('root')),
                nested: rect(document.getElementById('nested')),
                rootRow: parts('root-row'),
                nestedRow: parts('nested-row'),
                rootBody: body('root-body'),
                nestedBody: body('nested-body'),
                nestedBg: getComputedStyle(document.getElementById('nested')).backgroundColor,
                rootBg: getComputedStyle(document.getElementById('root')).backgroundColor
            };
        }
        """;

    private static double D(JsonElement e, string name) => e.GetProperty(name).GetDouble();

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_nested_group_is_indented_from_its_parent_on_the_inline_start(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture(dir, 760));
        var m = await page.EvaluateAsync<JsonElement>(Measure);

        var start = dir == "ltr" ? "left" : "right";
        var end = dir == "ltr" ? "right" : "left";
        var sign = dir == "ltr" ? 1.0 : -1.0;

        // The line down each body is on the inline-start edge, and only there.
        Assert.Equal("2px", m.GetProperty("rootBody").GetProperty(start).GetString());
        Assert.Equal("0px", m.GetProperty("rootBody").GetProperty(end).GetString());
        Assert.Equal("2px", m.GetProperty("nestedBody").GetProperty(start).GetString());

        // The nested group starts inside its parent's body, further along the inline axis…
        var rootStart = D(m.GetProperty("root"), start);
        var nestedStart = D(m.GetProperty("nested"), start);
        Assert.True((nestedStart - rootStart) * sign > 16, $"The nested group starts {nestedStart - rootStart}px from its parent.");
        // …and its row starts further along again than the parent's own row.
        var rootRowStart = D(m.GetProperty("rootRow")[0], start);
        var nestedRowStart = D(m.GetProperty("nestedRow")[0], start);
        Assert.True((nestedRowStart - rootRowStart) * sign > 16, $"A nested row starts {nestedRowStart - rootRowStart}px from its parent's.");
        // Both groups end at the same inline end: the indent is taken from the start only.
        Assert.True(Math.Abs(D(m.GetProperty("nested"), end) - D(m.GetProperty("root"), end)) < 40);

        // A nested group is set on its own surface.
        Assert.NotEqual(m.GetProperty("rootBg").GetString(), m.GetProperty("nestedBg").GetString());
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_wide_row_is_one_line_of_field_operator_value_and_remove(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture(dir, 760));
        var m = await page.EvaluateAsync<JsonElement>(Measure);

        var row = m.GetProperty("rootRow").EnumerateArray().ToList();
        Assert.Equal(4, row.Count);
        // One line: every part centred on the same line.
        var centre = (D(row[0], "top") + D(row[0], "bottom")) / 2;
        foreach (var part in row)
            Assert.Equal(centre, (D(part, "top") + D(part, "bottom")) / 2, 0.5);
        // In order along the inline axis, whichever way it runs.
        for (var i = 1; i < row.Count; i++)
        {
            if (dir == "ltr") Assert.True(D(row[i], "left") >= D(row[i - 1], "right"));
            else Assert.True(D(row[i], "right") <= D(row[i - 1], "left"));
        }
        // The value is the widest of the three fields.
        double Width(JsonElement r) => D(r, "right") - D(r, "left");
        Assert.True(Width(row[2]) > Width(row[0]));
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_narrow_group_stacks_its_fields_with_the_remove_button_beside_the_first(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture(dir, 360));
        var m = await page.EvaluateAsync<JsonElement>(Measure);

        foreach (var name in new[] { "rootRow", "nestedRow" })
        {
            var row = m.GetProperty(name).EnumerateArray().ToList();
            // Field, operator and value one under the other.
            Assert.True(D(row[1], "top") >= D(row[0], "bottom"), $"{name}: the operator is not under the field.");
            Assert.True(D(row[2], "top") >= D(row[1], "bottom"), $"{name}: the value is not under the operator.");
            // The remove button on the first line, at the inline end.
            Assert.Equal((D(row[0], "top") + D(row[0], "bottom")) / 2, (D(row[3], "top") + D(row[3], "bottom")) / 2, 0.5);
            if (dir == "ltr") Assert.True(D(row[3], "left") >= D(row[0], "right"));
            else Assert.True(D(row[3], "right") <= D(row[0], "left"));
        }

        // Nothing spills out of either group.
        foreach (var (group, rowName) in new[] { ("root", "rootRow"), ("nested", "nestedRow") })
        {
            var box = m.GetProperty(group);
            foreach (var part in m.GetProperty(rowName).EnumerateArray())
            {
                Assert.True(D(part, "left") >= D(box, "left") - 0.5, $"A part of {rowName} spills past the left of {group}.");
                Assert.True(D(part, "right") <= D(box, "right") + 0.5, $"A part of {rowName} spills past the right of {group}.");
            }
        }
    }
}
