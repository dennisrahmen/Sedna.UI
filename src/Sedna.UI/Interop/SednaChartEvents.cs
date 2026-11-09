namespace Sedna.UI;

/// <summary>
/// A point of a <c>[data-chart]</c> was chosen — clicked, tapped, or reached by keyboard and
/// pressed with Enter or Space: <c>sedna-chart-select</c>, dispatched on the chart's element.
/// </summary>
/// <remarks>
/// The chart only reports the choice. What it means — opening the month behind a column,
/// filtering a table to a slice — is the app's, done in its own data and re-rendered.
/// </remarks>
public sealed class SednaChartEventArgs : EventArgs
{
    /// <summary>The chart element's id, or empty when it has none.</summary>
    public string Chart { get; set; } = "";

    /// <summary>The series' name: its column heading in the chart's table.</summary>
    public string Series { get; set; } = "";

    /// <summary>The category's label: its row heading in the chart's table.</summary>
    public string Category { get; set; } = "";

    /// <summary>The value as a number, or null where the cell holds none.</summary>
    public double? Value { get; set; }

    /// <summary>The value as the app wrote it — the cell's text.</summary>
    public string Text { get; set; } = "";

    /// <summary>The category's position in the data, from 0.</summary>
    public int Index { get; set; }

    /// <summary>The series' position in the data, from 0.</summary>
    public int SeriesIndex { get; set; }
}
