namespace Sedna.UI;

/// <summary>
/// A <c>&lt;time data-countdown&gt;</c> reached zero: <c>sedna-countdown-end</c>, dispatched on
/// the element.
/// </summary>
/// <remarks>
/// It fires once per element and <c>datetime</c>: when the countdown reaches zero, when it is
/// found already over, and when the page is shown again after it ended while hidden. Render the
/// element with a later <c>datetime</c> and it counts down, and fires, again.
/// </remarks>
public sealed class SednaCountdownEventArgs : EventArgs
{
    /// <summary>The element's <c>datetime</c>, as the app wrote it.</summary>
    public string Until { get; set; } = "";
}
