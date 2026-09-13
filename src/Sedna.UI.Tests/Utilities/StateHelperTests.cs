namespace Sedna.UI.Tests;

/// <summary>
/// The state helpers: <see cref="SednaSort"/>, <see cref="SednaPager"/> and
/// <see cref="SednaTabs"/>. Pure functions over the app's own state, so every case is a
/// value in and a value out.
/// </summary>
public class StateHelperTests
{
    // ── SednaSort ───────────────────────────────────────────────────────────

    [Fact]
    public void Aria_sort_names_the_direction_on_the_sorted_column_and_none_elsewhere()
    {
        var sort = SednaSort.By("due", descending: true);

        Assert.Equal("descending", sort.AriaSort("due"));
        // "none", never an absent attribute: it is what shows the hover arrow and what tells
        // a screen reader the column can be sorted.
        Assert.Equal("none", sort.AriaSort("owner"));
        Assert.Equal("none", SednaSort.None.AriaSort("due"));
    }

    [Fact]
    public void Toggle_starts_a_new_column_ascending_and_cycles_the_sorted_one()
    {
        var sort = SednaSort.None.Toggle("due");
        Assert.Equal(("due", false), (sort.Column, sort.Descending));

        sort = sort.Toggle("due");
        Assert.Equal(("due", true), (sort.Column, sort.Descending));

        Assert.Equal(("due", false), (sort.Toggle("due").Column, sort.Toggle("due").Descending));
        Assert.Same(SednaSort.None, sort.Toggle("due", thenNone: true));

        // A different column never inherits the direction.
        Assert.Equal(("owner", false), (sort.Toggle("owner").Column, sort.Toggle("owner").Descending));
    }

    [Theory]
    [InlineData("due", "due", false)]
    [InlineData("-due", "due", true)]
    [InlineData("", null, false)]
    [InlineData("-", null, false)]
    [InlineData(null, null, false)]
    public void A_query_value_round_trips(string? value, string? column, bool descending)
    {
        var sort = SednaSort.FromQuery(value);

        Assert.Equal((column, descending), (sort.Column, sort.Descending));
        Assert.Equal(column is null ? null : value, sort.ToQuery());
    }

    // ── SednaPager ──────────────────────────────────────────────────────────

    private static string Render(IReadOnlyList<PagerSlot> slots) =>
        string.Join(" ", slots.Select(s => s.IsGap ? "…" : s.IsCurrent ? $"[{s.Number}]" : s.Number.ToString()));

    [Theory]
    [InlineData(1, 1, "[1]")]
    [InlineData(1, 5, "[1] 2 3 4 5")]
    // Seven slots on every page of twenty: the window slides to an end, it does not shrink.
    [InlineData(1, 20, "[1] 2 3 4 5 … 20")]
    [InlineData(5, 20, "1 … 4 [5] 6 … 20")]
    [InlineData(20, 20, "1 … 16 17 18 19 [20]")]
    // A gap never stands for one page: 1 … 3 would hide only 2, which is no shorter.
    [InlineData(4, 20, "1 2 3 [4] 5 … 20")]
    [InlineData(17, 20, "1 … 16 [17] 18 19 20")]
    // Clamped, not thrown: a stale page number after a filter shrank the result.
    [InlineData(99, 3, "1 2 [3]")]
    [InlineData(0, 3, "[1] 2 3")]
    public void The_window_shows_the_ends_the_neighbours_and_real_gaps(int current, int total, string expected) =>
        Assert.Equal(expected, Render(SednaPager.Window(current, total)));

    [Fact]
    public void Wider_edges_and_neighbourhoods_are_honoured()
    {
        Assert.Equal("1 2 … 8 9 [10] 11 12 … 19 20", Render(SednaPager.Window(10, 20, edge: 2, around: 2)));
        Assert.Empty(SednaPager.Window(1, 0));
    }

    // ── SednaTabs ───────────────────────────────────────────────────────────

    [Fact]
    public void A_tab_and_its_panel_name_each_other_and_only_the_selected_one_is_a_tab_stop()
    {
        var open = SednaTabs.Tab("open", "open", "queue");
        var all = SednaTabs.Tab("all", "open", "queue");
        var openPanel = SednaTabs.Panel("open", "open", "queue");
        var allPanel = SednaTabs.Panel("all", "open", "queue");

        Assert.Equal(("true", "0"), (open["aria-selected"], open["tabindex"]));
        Assert.Equal(("false", "-1"), (all["aria-selected"], all["tabindex"]));
        Assert.Equal(open["aria-controls"], openPanel["id"]);
        Assert.Equal(open["id"], openPanel["aria-labelledby"]);

        // A bool, so Blazor omits the attribute on the visible panel instead of rendering
        // hidden="false", which would still hide it.
        Assert.Equal(false, openPanel["hidden"]);
        Assert.Equal(true, allPanel["hidden"]);
    }
}
