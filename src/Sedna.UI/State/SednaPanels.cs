namespace Sedna.UI;

/// <summary>
/// The arrangement of a <c>.panel-grid</c>: where every panel settles once one has been
/// moved or resized, with nothing overlapping and nothing hanging over a gap.
/// </summary>
/// <remarks>
/// <para>
/// A state helper: one pure function, no interop and nothing held. It is the same function as
/// <c>sednaUi.panels.arrange</c>, which the script previews a drag with and whose result the
/// <c>sedna-drop</c> and <c>sedna-resize</c> events carry, so an app that adds a panel, changes
/// the column count or loads an arrangement it did not make itself settles it here the way the
/// reader's own moves are settled:
/// </para>
/// <code>
/// _panels = SednaPanels.Arrange([.. _panels, new("returns", 0, 0, 4, 3)], columns: 12);
///
/// @foreach (var p in _panels)
/// {
///     &lt;section class="card card--resizable" @key="p.Id" data-drag-item="@p.Id"
///              style="--panel-x: @p.X; --panel-y: @p.Y; --panel-w: @p.W; --panel-h: @p.H"&gt;…&lt;/section&gt;
/// }
/// </code>
/// <para>
/// A panel in the way of the moved one goes down below it; then every panel floats up until it
/// rests on the top or on a panel above it. The moved panel takes its place against a panel on
/// the same row, unless it was moving down, when it goes below.
/// </para>
/// </remarks>
public static class SednaPanels
{
    /// <summary>The arrangement <paramref name="panels"/> settles into.</summary>
    /// <param name="panels">Every panel, in cells. A second panel with an id already seen is left out.</param>
    /// <param name="columns">How many columns the grid has: its <c>--panel-cols</c>. At least one.</param>
    /// <param name="moved">
    /// A panel put where it says first — the one the reader moved or resized, or one being added.
    /// Its id names the panel it replaces; a new id adds it. Null only settles the rest.
    /// </param>
    /// <returns>
    /// New panels in reading order — by row, then by column — which is the order to render them
    /// in, so the tab order follows the screen. Each is inside the grid: at least one cell, no
    /// wider than the grid, not past its end.
    /// </returns>
    public static IReadOnlyList<SednaPanel> Arrange(IEnumerable<SednaPanel> panels, int columns, SednaPanel? moved = null)
    {
        ArgumentNullException.ThrowIfNull(panels);
        var cols = Math.Max(1, columns);

        var list = new List<Cell>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in panels)
        {
            if (p is null || !seen.Add(p.Id)) continue;
            list.Add(Cell.Fit(p, cols));
        }

        Cell? mover = null;
        var down = false;
        if (moved is not null)
        {
            mover = Cell.Fit(moved, cols);
            var at = list.FindIndex(c => c.Id == moved.Id);
            if (at == -1) list.Add(mover);
            else
            {
                down = mover.Y > list[at].Y;
                list[at] = mover;
            }
        }

        // The order panels settle in: by row, then column. The moved one goes before a panel
        // on its row — it takes that place — unless it was moving down, when it goes after.
        // A stable sort, as the script's is, so two panels on one cell settle in the same order.
        var order = list.Order(Comparer<Cell>.Create((a, b) =>
        {
            if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
            if (ReferenceEquals(a, mover) || ReferenceEquals(b, mover))
                return ReferenceEquals(a, mover) == down ? 1 : -1;
            return a.X.CompareTo(b.X);
        })).ToList();

        var placed = new List<Cell>(order.Count);
        foreach (var p in order)
        {
            var y = p.Y;
            // Down below anything already in the way…
            for (var hit = Blocker(p, y, placed); hit is not null; hit = Blocker(p, y, placed)) y = hit.Y + hit.H;
            // …then up until it rests on the top or on a panel.
            while (y > 0 && Blocker(p, y - 1, placed) is null) y--;
            p.Y = y;
            placed.Add(p);
        }

        return placed
            .OrderBy(c => c.Y).ThenBy(c => c.X)
            .Select(c => new SednaPanel(c.Id, c.X, c.Y, c.W, c.H))
            .ToList();
    }

    private static Cell? Blocker(Cell p, int y, List<Cell> placed)
    {
        foreach (var q in placed)
        {
            if (p.X < q.X + q.W && q.X < p.X + p.W && y < q.Y + q.H && q.Y < y + p.H) return q;
        }
        return null;
    }

    // Mutable while it settles; nothing outside sees one.
    private sealed class Cell(string id, int x, int y, int w, int h)
    {
        public string Id { get; } = id;
        public int X { get; } = x;
        public int Y { get; set; } = y;
        public int W { get; } = w;
        public int H { get; } = h;

        public static Cell Fit(SednaPanel p, int cols)
        {
            var w = Math.Min(cols, Math.Max(1, p.W));
            return new Cell(p.Id, Math.Min(cols - w, Math.Max(0, p.X)), Math.Max(0, p.Y), w, Math.Max(1, p.H));
        }
    }
}

/// <summary>
/// One panel of a <c>.panel-grid</c>, in cells counted from 0 at the inline start and the top:
/// what the app stores per reader and writes back as <c>--panel-x</c>, <c>--panel-y</c>,
/// <c>--panel-w</c> and <c>--panel-h</c>.
/// </summary>
/// <param name="Id">The panel's <c>data-drag-item</c>.</param>
/// <param name="X">The first column it covers.</param>
/// <param name="Y">The first row it covers.</param>
/// <param name="W">How many columns it covers.</param>
/// <param name="H">How many rows it covers.</param>
public sealed record SednaPanel(string Id, int X, int Y, int W, int H);
