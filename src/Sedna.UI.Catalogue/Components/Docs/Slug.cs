using System.Text;

namespace Sedna.UI.Catalogue.Components.Docs;

/// <summary>
/// Turns an example's title into a heading id, so every section can be linked to
/// directly.
/// </summary>
/// <remarks>
/// Server-rendered, which is why the anchors work with scripting off — the static
/// catalogue built them in JavaScript and a reader with no script had no way to
/// link to a section.
/// </remarks>
internal static class Slug
{
    public static string From(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var text = new StringBuilder(title.Length);
        var pendingSeparator = false;

        foreach (var c in title)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSeparator && text.Length > 0) text.Append('-');
                pendingSeparator = false;
                text.Append(char.ToLowerInvariant(c));
            }
            else
            {
                // Collapsed rather than emitted: "Tabs & segmented" is one dash,
                // not three.
                pendingSeparator = true;
            }
        }

        return text.Length == 0 ? "section" : text.ToString();
    }
}
