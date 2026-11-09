namespace Sedna.UI;

/// <summary>
/// A handle finished resizing what it resizes: <c>sedna-resize</c>, dispatched on the element
/// whose custom property the handle writes — a resizable card, a split, a table column's header cell.
/// </summary>
/// <remarks>
/// It fires once a drag ends, on each arrow key, and on a reset, so an app can keep the size
/// the reader chose and write it back as the same property next time.
/// </remarks>
public sealed class SednaResizeEventArgs : EventArgs
{
    /// <summary>The custom property the handle writes, e.g. <c>--card-height</c>.</summary>
    public string Property { get; set; } = "";

    /// <summary>The size in pixels, or null once the reader put it back to the default.</summary>
    public double? Value { get; set; }

    /// <summary>
    /// True when the reader dragged it below the handle's <c>data-resize-fold</c>, or pressed an
    /// arrow towards smaller at its minimum: the app folds it — a card takes <c>.card--folded</c>.
    /// The size is back at what it was before, and <see cref="Value"/> says what that is.
    /// </summary>
    public bool Folded { get; set; }

    /// <summary>
    /// The handle's <c>data-resize-key</c> — which column of a <c>.table--fixed</c> a
    /// <c>.col-resize</c> grip sized — or null when it has none.
    /// </summary>
    public string? Key { get; set; }
}
