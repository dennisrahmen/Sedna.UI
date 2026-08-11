using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every colour resolves through a token, so an app can rebrand by redefining tokens.
/// </summary>
public class ColourTests
{
    // Colour literals. `transparent` and `currentColor` are allowed: neither
    // pins a value that a theme would need to change.
    private static readonly Regex HexColour = new(@"#[0-9a-fA-F]{3,8}\b", RegexOptions.Compiled);
    // color-mix and light-dark are listed explicitly: `color` alone does not match
    // them, because the hyphen sits where this pattern expects the paren. Both are
    // legitimate inside a token block and must not leak outside one.
    private static readonly Regex ColourFunction = new(
        @"\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color-mix|light-dark|color)\s*\(",
        RegexOptions.Compiled);
    // The lookarounds reject hyphens as well as word characters, so `white-space`,
    // `--badge-cyan-bg` and `border-color` are not mistaken for colour keywords.
    private static readonly Regex NamedColour = new(
        @"(?<![\w-])(?:white|black|red|green|blue|gray|grey|orange|yellow|purple|cyan|teal|" +
        @"magenta|silver|navy|olive|lime|maroon|aqua|fuchsia|pink|brown|gold|beige|" +
        @"ivory|khaki|salmon|tan|violet|indigo|crimson|tomato)(?![\w-])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // CSS system colours. Legal ONLY as a token remap inside
    // @media (forced-colors: active) — which lands inside a :root block and is
    // therefore masked out before this scan. Anywhere else they pin a value a theme
    // cannot change, exactly like a hex.
    //
    // Matched case-sensitively on the conventional CamelCase spelling, deliberately.
    // CSS keywords are case-insensitive, but matching case-insensitively would make
    // `mark { … }` and `field-sizing` collide with Mark and Field. The authoritative
    // check on placement is Appearance_media_queries_only_remap_tokens; this is the
    // second line of defence.
    private static readonly Regex SystemColour = new(
        @"(?<![\w-])(?:Canvas|CanvasText|ButtonFace|ButtonText|ButtonBorder|LinkText|" +
        @"VisitedText|ActiveText|GrayText|Highlight|HighlightText|SelectedItem|" +
        @"SelectedItemText|AccentColor|AccentColorText)(?![\w-])",
        RegexOptions.Compiled);

    [Fact]
    public void No_hard_coded_colours_outside_the_token_blocks()
    {
        var css = Assets.StripComments(Assets.Css);

        // Inside a forced-colors block the system palette IS the token layer: the
        // browser has already replaced every colour with the user's choice, and
        // `CanvasText` is the name of one of them. A focus outline there cannot go
        // through a var() — box-shadow is not painted in that mode, so the ring has to
        // be a real outline in a real system colour. Everywhere else the keywords stay
        // banned, which is what the line-range check preserves.
        var forcedColourLines = ForcedColourLineNumbers(css);

        var offenders = new List<string>();
        foreach (var (line, number) in Assets.LinesOutsideTokenBlocks(css))
        {
            if (HexColour.IsMatch(line)) offenders.Add($"line {number}: hex — {line.Trim()}");
            else if (ColourFunction.IsMatch(line)) offenders.Add($"line {number}: colour function — {line.Trim()}");
            else if (NamedColour.IsMatch(line)) offenders.Add($"line {number}: named colour — {line.Trim()}");
            else if (SystemColour.IsMatch(line) && !forcedColourLines.Contains(number))
                offenders.Add($"line {number}: system colour — {line.Trim()}");
        }

        Assert.True(offenders.Count == 0,
            "Every colour in the library must resolve through a token, or an app cannot rebrand " +
            "by redefining tokens. Move these into the :root blocks and reference them with " +
            $"var(--…):{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    /// <summary>
    /// The 1-based line numbers that fall inside a <c>@media (forced-colors: active)</c>
    /// block, so the system-colour keywords may be recognised there and nowhere else.
    /// </summary>
    private static HashSet<int> ForcedColourLineNumbers(string css)
    {
        var inside = new HashSet<int>();

        foreach (var open in Regex.Matches(css, @"@media[^{]*forced-colors[^{]*\{").Cast<Match>())
        {
            var depth = 1;
            var i = open.Index + open.Length;
            var line = css.Take(open.Index).Count(c => c == '\n') + 1;

            while (i < css.Length && depth > 0)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}') depth--;
                else if (css[i] == '\n') { line++; inside.Add(line); }
                i++;
            }
        }

        return inside;
    }
}
