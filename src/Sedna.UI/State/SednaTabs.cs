namespace Sedna.UI;

/// <summary>
/// The attributes a tab and its panel need when the app keeps the selection — for a
/// <c>data-tabs="managed"</c> tablist.
/// </summary>
/// <remarks>
/// <para>
/// A state helper: pure functions over the app's own selected key, no interop and nothing
/// held. The script supplies the keyboard — arrows, Home and End move focus to a tab and
/// click it — and the app's click handler changes the key. These render what follows from
/// it, so the selection, the tab order and the visible panel cannot disagree:
/// </para>
/// <code>
/// &lt;div class="tabs" role="tablist" data-tabs="managed"&gt;
///     &lt;button class="tab" type="button" @attributes="SednaTabs.Tab("open", _tab)" @onclick="() =&gt; _tab = "open""&gt;Open&lt;/button&gt;
///     &lt;button class="tab" type="button" @attributes="SednaTabs.Tab("all", _tab)" @onclick="() =&gt; _tab = "all""&gt;All&lt;/button&gt;
/// &lt;/div&gt;
/// &lt;div class="tab-panel" @attributes="SednaTabs.Panel("open", _tab)"&gt;…&lt;/div&gt;
/// &lt;div class="tab-panel" @attributes="SednaTabs.Panel("all", _tab)"&gt;…&lt;/div&gt;
/// </code>
/// <para>
/// Ids are <c>{prefix}-tab-{key}</c> and <c>{prefix}-panel-{key}</c>, so a tab and its panel
/// name each other. Give each tablist on a page its own <c>prefix</c>.
/// </para>
/// </remarks>
public static class SednaTabs
{
    /// <summary>A tab's attributes: role, id, <c>aria-controls</c>, <c>aria-selected</c> and the roving <c>tabindex</c>.</summary>
    /// <param name="key">This tab's key.</param>
    /// <param name="selected">The app's selected key.</param>
    /// <param name="prefix">Keeps the ids of two tablists on one page apart.</param>
    /// <returns>The attributes, for <c>@attributes</c>.</returns>
    /// <remarks>
    /// Only the selected tab is a tab stop (<c>tabindex="0"</c>), so <kbd>Tab</kbd> moves
    /// past the whole tablist; the arrows move within it.
    /// </remarks>
    public static IReadOnlyDictionary<string, object> Tab(string key, string? selected, string prefix = "tabs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var isSelected = string.Equals(key, selected, StringComparison.Ordinal);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["role"] = "tab",
            ["id"] = $"{prefix}-tab-{key}",
            ["aria-controls"] = $"{prefix}-panel-{key}",
            ["aria-selected"] = isSelected ? "true" : "false",
            ["tabindex"] = isSelected ? "0" : "-1",
        };
    }

    /// <summary>A panel's attributes: role, id, <c>aria-labelledby</c>, and <c>hidden</c> unless it is selected.</summary>
    /// <param name="key">This panel's key — the same as its tab's.</param>
    /// <param name="selected">The app's selected key.</param>
    /// <param name="prefix">The same prefix its tab was given.</param>
    /// <returns>The attributes, for <c>@attributes</c>.</returns>
    public static IReadOnlyDictionary<string, object> Panel(string key, string? selected, string prefix = "tabs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["role"] = "tabpanel",
            ["id"] = $"{prefix}-panel-{key}",
            ["aria-labelledby"] = $"{prefix}-tab-{key}",
            // A bool, not a string: Blazor omits a false boolean attribute entirely,
            // where the string "false" would still hide the panel.
            ["hidden"] = !string.Equals(key, selected, StringComparison.Ordinal),
        };
    }
}
