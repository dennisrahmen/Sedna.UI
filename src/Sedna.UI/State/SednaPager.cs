namespace Sedna.UI;

/// <summary>
/// Which page numbers a <c>.pager</c> shows: <c>1 … 4 5 6 … 20</c>, with the window
/// shrinking near either end and the gaps in the right places.
/// </summary>
/// <remarks>
/// <para>
/// A state helper: one pure function, no interop and nothing held. The markup stays a
/// <c>foreach</c> in the app:
/// </para>
/// <code>
/// @foreach (var slot in SednaPager.Window(_page, _pages))
/// {
///     if (slot.IsGap) { &lt;span class="page-gap"&gt;…&lt;/span&gt; continue; }
///     &lt;button class="page-link" type="button" aria-current="@(slot.IsCurrent ? "page" : null)"
///             @onclick="() =&gt; _page = slot.Number"&gt;@slot.Number&lt;/button&gt;
/// }
/// </code>
/// </remarks>
public static class SednaPager
{
    /// <summary>The page numbers and gaps to show, in order.</summary>
    /// <param name="current">The current page, from 1. Clamped into range.</param>
    /// <param name="total">How many pages there are. Zero or less yields nothing.</param>
    /// <param name="edge">How many pages always show at each end.</param>
    /// <param name="around">How many pages show either side of the current one.</param>
    /// <returns>
    /// The slots. Once there are enough pages to need a gap, the count of slots is the same
    /// on every page — the window slides towards an end instead of shrinking there — so the
    /// buttons do not move under the pointer as the reader pages. A gap never stands for a
    /// single page: that page is shown instead, since an ellipsis is no shorter than it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edge"/> or <paramref name="around"/> is negative.</exception>
    public static IReadOnlyList<PagerSlot> Window(int current, int total, int edge = 1, int around = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edge);
        ArgumentOutOfRangeException.ThrowIfNegative(around);
        if (total <= 0) return [];

        current = Math.Clamp(current, 1, total);
        var slots = new List<PagerSlot>();

        for (var n = 1; n <= Math.Min(edge, total); n++) slots.Add(PagerSlot.Page(n, current));

        // The run around the current page, pushed inward at either end so it keeps its
        // length: on page 1 it covers 2…5 rather than 1…2.
        var start = Math.Max(Math.Min(current - around, total - edge - around * 2 - 1), edge + 2);
        var end = Math.Min(Math.Max(current + around, edge + around * 2 + 2), total - edge - 1);

        if (start > edge + 2) slots.Add(PagerSlot.Gap);
        else if (edge + 1 < total - edge) slots.Add(PagerSlot.Page(edge + 1, current));

        for (var n = start; n <= end; n++) slots.Add(PagerSlot.Page(n, current));

        if (end < total - edge - 1) slots.Add(PagerSlot.Gap);
        else if (total - edge > edge) slots.Add(PagerSlot.Page(total - edge, current));

        for (var n = Math.Max(total - edge + 1, edge + 1); n <= total; n++) slots.Add(PagerSlot.Page(n, current));

        return slots;
    }
}

/// <summary>One slot in a pager: a page number, or a gap standing for several.</summary>
/// <param name="Number">The page number; 0 for a gap.</param>
/// <param name="IsGap">A gap, rendered as <c>.page-gap</c>.</param>
/// <param name="IsCurrent">The current page, which carries <c>aria-current="page"</c>.</param>
public readonly record struct PagerSlot(int Number, bool IsGap, bool IsCurrent)
{
    /// <summary>A gap.</summary>
    public static PagerSlot Gap { get; } = new(0, true, false);

    internal static PagerSlot Page(int number, int current) => new(number, false, number == current);
}
