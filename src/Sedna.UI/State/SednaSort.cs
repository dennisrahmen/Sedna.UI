namespace Sedna.UI;

/// <summary>
/// Which column a table is sorted by, and in which direction — the one thing a
/// <c>.th-sort</c> header cannot express in markup.
/// </summary>
/// <remarks>
/// <para>
/// A state helper: an immutable value and pure functions, no interop and nothing held. The
/// sorting itself is the app's query; this only renders <c>aria-sort</c>, which is both what
/// the header's arrow follows and what a screen reader announces, so the two cannot disagree.
/// </para>
/// <code>
/// &lt;th aria-sort="@_sort.AriaSort("due")"&gt;
///     &lt;button class="th-sort" type="button" @onclick="() =&gt; _sort = _sort.Toggle("due")"&gt;Due&lt;/button&gt;
/// &lt;/th&gt;
///
/// @code { private SednaSort _sort = SednaSort.By("due"); }
/// </code>
/// </remarks>
public sealed record SednaSort
{
    private SednaSort(string? column, bool descending)
    {
        Column = column;
        Descending = descending;
    }

    /// <summary>Unsorted: every sortable header reads <c>aria-sort="none"</c>.</summary>
    public static SednaSort None { get; } = new(null, false);

    /// <summary>The sorted column's key, or <see langword="null"/> when unsorted.</summary>
    public string? Column { get; }

    /// <summary>Whether the sort is descending. Always false when unsorted.</summary>
    public bool Descending { get; }

    /// <summary>Sorted by <paramref name="column"/>.</summary>
    /// <param name="column">The column's key — whatever the app's query understands.</param>
    /// <param name="descending">Descending rather than ascending.</param>
    /// <returns>The sort.</returns>
    public static SednaSort By(string column, bool descending = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        return new SednaSort(column, descending);
    }

    /// <summary>Whether <paramref name="column"/> is the sorted one.</summary>
    /// <param name="column">The column's key.</param>
    /// <returns><see langword="true"/> for the sorted column.</returns>
    public bool Is(string column) => string.Equals(Column, column, StringComparison.Ordinal);

    /// <summary>
    /// The <c>aria-sort</c> value for <paramref name="column"/>'s header:
    /// <c>ascending</c>, <c>descending</c> or <c>none</c>.
    /// </summary>
    /// <param name="column">The column's key.</param>
    /// <returns>The attribute value.</returns>
    /// <remarks>
    /// Put it on every sortable header, the unsorted ones included. <c>none</c> is what shows
    /// the faint arrow on hover, and what tells a screen reader the column can be sorted.
    /// </remarks>
    public string AriaSort(string column) =>
        !Is(column) ? "none" : Descending ? "descending" : "ascending";

    /// <summary>
    /// The sort after <paramref name="column"/>'s header is clicked: a new column starts
    /// ascending, and the sorted column turns to descending.
    /// </summary>
    /// <param name="column">The clicked column's key.</param>
    /// <param name="thenNone">
    /// A descending column goes back to unsorted rather than to ascending — for a table whose
    /// natural order means something, such as arrival.
    /// </param>
    /// <returns>The new sort.</returns>
    public SednaSort Toggle(string column, bool thenNone = false)
    {
        if (!Is(column)) return By(column);
        if (!Descending) return By(column, descending: true);
        return thenNone ? None : By(column);
    }

    /// <summary>
    /// The sort as a query-string value: the key for ascending, <c>-key</c> for descending,
    /// and <see langword="null"/> for unsorted, so an unsorted table leaves no parameter.
    /// </summary>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? ToQuery() =>
        Column is null ? null : Descending ? "-" + Column : Column;

    /// <summary>Reads a value written by <see cref="ToQuery"/>, as it arrives from a query string.</summary>
    /// <param name="value">The value, which may be null or empty.</param>
    /// <returns>The sort; <see cref="None"/> for null, empty or a bare <c>-</c>.</returns>
    public static SednaSort FromQuery(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return None;
        var descending = value[0] == '-';
        var column = descending ? value[1..] : value;
        return string.IsNullOrWhiteSpace(column) ? None : By(column, descending);
    }
}
