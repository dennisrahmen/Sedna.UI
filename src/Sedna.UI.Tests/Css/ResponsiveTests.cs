using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Layout media queries move geometry only, and the responsive rail mirrors the collapsed one.
/// </summary>
public class ResponsiveTests
{
    /// <summary>
    /// Properties a layout media query may set freely. Everything else has to come
    /// from a token, so appearance stays decided in one place.
    /// </summary>
    private static readonly HashSet<string> GeometryProperties = new(StringComparer.Ordinal)
    {
        "width", "min-width", "max-width", "height", "min-height", "max-height",
        "aspect-ratio", "object-fit", "visibility", "content", "pointer-events",
        "clip-path", "border-spacing", "border-collapse",
        // Print flow, and the outlines forced colours needs.
        "break-inside", "break-before", "break-after", "word-break", "overflow-wrap",
        "outline", "outline-offset", "table-layout",
        "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
        "padding-block", "padding-inline", "padding-inline-start", "padding-inline-end",
        "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
        "margin-block", "margin-inline", "margin-inline-start", "margin-inline-end",
        "display", "flex", "flex-direction", "flex-wrap", "flex-shrink", "flex-grow",
        "flex-basis", "align-items", "align-self", "justify-content", "order",
        "gap", "row-gap", "column-gap",
        "grid-template-columns", "grid-template-rows", "grid-column", "grid-row",
        "position", "top", "right", "bottom", "left",
        "inset", "inset-block", "inset-inline", "inset-block-start", "inset-block-end",
        "inset-inline-start", "inset-inline-end",
        "transform", "translate", "rotate", "scale",
        // Anchor positioning. Every one of these answers "where does this box
        // go", which is the same question `top` and `inset` answer — the value
        // being a --dashed-ident rather than a length does not make it a
        // decision about appearance.
        "anchor-name", "anchor-scope", "position-anchor", "position-area",
        "position-try-fallbacks", "position-visibility",
        "overflow", "overflow-x", "overflow-y", "z-index",
        "border-radius", "border-width", "border-style",
        "font-size", "font-weight", "line-height", "letter-spacing",
        "text-align", "text-transform", "white-space", "text-overflow",
        // Switching motion off is not an appearance decision.
        "transition", "animation"
    };

    [Fact]
    public void Layout_media_queries_only_change_geometry()
    {
        // A width query answers "how much room is there", never "what should this
        // look like". Appearance is decided once, in the token blocks, so that a
        // rebrand is a token edit and a theme is a token remap — a colour set inside
        // a breakpoint is invisible to both and reappears at one window size.
        //
        // A non-geometry property is still allowed when its value comes entirely from
        // tokens: the collapsed rail's flyout has to restate its surface, and doing so
        // through var() keeps the decision in the token block where it belongs.
        //
        // Scoped by an allowlist of conditions rather than by excluding appearance
        // ones, so a new kind of media query is not silently waved through. The
        // others are each somebody else's business: appearance queries have their own
        // stricter guard above, `prefers-reduced-motion` exists to turn motion off,
        // and a capability query (`hover`, `pointer`) legitimately reveals a control
        // that hover would otherwise have revealed.
        string[] layoutFeatures = ["width", "height", "orientation", "print"];

        var css = Assets.StripComments(Assets.Css);
        var offenders = new List<string>();

        foreach (var (condition, body) in Assets.MediaBlocks(css))
        {
            if (!layoutFeatures.Any(f => condition.Contains(f, StringComparison.OrdinalIgnoreCase)))
                continue;

            foreach (var (selector, ruleBody) in Assets.TopLevelRules(body))
            {
                foreach (var declaration in ruleBody.Split(';', StringSplitOptions.TrimEntries))
                {
                    if (declaration.Length == 0) continue;

                    var colon = declaration.IndexOf(':', StringComparison.Ordinal);
                    if (colon <= 0) continue;

                    var property = declaration[..colon].Trim();
                    var value = declaration[(colon + 1)..].Trim();

                    if (property.StartsWith("--", StringComparison.Ordinal)) continue;
                    if (GeometryProperties.Contains(property)) continue;
                    // Token-only values are fine: the decision still lives in :root.
                    if (value.Contains("var(--", StringComparison.Ordinal)) continue;
                    if (value is "transparent" or "currentColor" or "none" or "inherit") continue;

                    offenders.Add($"@media {condition} → {selector} → {property}: {value}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "A layout media query may only change geometry, or set a property whose value comes from a " +
            "token. Anything else decides appearance at one window size, where neither a rebrand nor a " +
            "theme remap can reach it:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void The_responsive_frame_mirrors_the_collapsed_rail()
    {
        // 19-frame-responsive.css repeats 12-frame-collapsed-rail.css with a
        // different trigger, because CSS cannot alias a selector: there is no way to
        // say "also apply the rail when this media query matches". Duplication is
        // therefore forced, and duplication drifts — a rule added to the rail and not
        // to the responsive arm means the narrow-screen rail is subtly broken, on a
        // width nobody develops at.
        var rail = Assets.StripComments(ReadPart("12-frame-collapsed-rail.css"));
        var responsive = Assets.StripComments(ReadPart("19-frame-responsive.css"));

        // The rail part has no media wrapper; the responsive part is entirely inside
        // one, so its rules are read out of the block bodies.
        var railSelectors = Selectors(rail, ".sidebar.collapsed");

        var responsiveSelectors = Assets.MediaBlocks(responsive)
            .SelectMany(m => Selectors(m.Body, ".layout--responsive .sidebar"))
            .ToHashSet(StringComparer.Ordinal);

        var missing = railSelectors.Except(responsiveSelectors).ToList();
        var extra = responsiveSelectors.Except(railSelectors).ToList();

        Assert.True(missing.Count == 0 && extra.Count == 0,
            "12-frame-collapsed-rail.css and 19-frame-responsive.css must cover the same selectors, or "
            + "the forced rail below 900px behaves differently from the toggled one."
            + (missing.Count > 0 ? $"{Environment.NewLine}Only in the rail: {string.Join(", ", missing)}" : "")
            + (extra.Count > 0 ? $"{Environment.NewLine}Only in the responsive arm: {string.Join(", ", extra)}" : ""));
    }

    private static string ReadPart(string name) =>
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", name));

    /// <summary>
    /// Every individual selector in a block, with <paramref name="trigger"/> replaced
    /// by a placeholder so the two triggers compare equal. Comma-grouped selectors are
    /// split apart, so regrouping rules is not mistaken for a change. Selectors that
    /// never mention the trigger — the responsive arm also hides parts of the user
    /// widget, which the rail has no counterpart for — are dropped.
    /// </summary>
    private static HashSet<string> Selectors(string blockBody, string trigger) =>
        Assets.TopLevelRules(blockBody)
            .SelectMany(r => r.Selector.Split(','))
            .Select(s => Regex.Replace(s.Trim(), @"\s+", " "))
            .Where(s => s.StartsWith(trigger, StringComparison.Ordinal))
            .Select(s => "«rail»" + s[trigger.Length..])
            .ToHashSet(StringComparer.Ordinal);
}
