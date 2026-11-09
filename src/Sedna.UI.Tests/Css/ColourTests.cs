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

    /// <summary>
    /// The one part allowed to hold literal colours: the colour picker's spectrum. A spectrum
    /// is a fact about colour rather than a brand decision — red is at hue 0 in every theme —
    /// so its values are fixed, and each is declared once as a <c>--spectrum-*</c> property.
    /// </summary>
    public const string SpectrumPart = "45-colour-picker.css";

    /// <summary>The prefix every spectrum property carries, and the only place a literal may sit.</summary>
    public const string SpectrumPrefix = "--spectrum-";

    private static readonly Regex SpectrumDeclaration = new(
        @"^\s*--spectrum-[a-z0-9-]+\s*:", RegexOptions.Compiled);

    // `── 45-colour-picker.css → @layer sedna.paint ──`, the marker the bundler writes before
    // each part in the readable copy.
    private static readonly Regex PartMarker = new(
        @"^/\* ── (?<part>\d\d-[^ ]+\.css) → @layer", RegexOptions.Compiled);

    private static string ReadableCss => File.ReadAllText(Assets.Readable("Sedna.UI.css"));

    [Fact]
    public void No_hard_coded_colours_outside_the_token_blocks()
    {
        var offenders = Offenders(ReadableCss);

        Assert.True(offenders.Count == 0,
            "Every colour in the library must resolve through a token, or an app cannot rebrand " +
            "by redefining tokens. Move these into the :root blocks and reference them with " +
            $"var(--…). The one exception is a {SpectrumPrefix}* declaration in {SpectrumPart}:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Theory]
    // A literal in an ordinary part.
    [InlineData("45-form-controls.css", ".x { color: #123456; }")]
    // A spectrum name is no pass outside the spectrum's part.
    [InlineData("45-form-controls.css", "    --spectrum-sky: #87ceeb;")]
    // Inside the spectrum's part, a literal outside a --spectrum-* declaration.
    [InlineData(SpectrumPart, ".form-color-area { border-color: rgb(0 0 0); }")]
    [InlineData(SpectrumPart, ".form-color-area { color: white; }")]
    public void The_spectrum_exception_admits_nothing_else(string part, string rule)
    {
        // The guard runs on the shipped stylesheet with one rule added to one part, which is
        // what proves the exception is as narrow as it says: it must still fail.
        var lines = ReadableCss.Split('\n').ToList();
        var marker = lines.FindIndex(l => l.StartsWith($"/* ── {part} →", StringComparison.Ordinal));
        Assert.True(marker >= 0, $"No marker for {part} in the readable stylesheet.");
        // After the `@layer … {` line that opens the part.
        lines.Insert(marker + 2, rule);

        var offenders = Offenders(string.Join('\n', lines));

        Assert.True(offenders.Count == 1, $"Expected the added rule alone to be reported, got: {string.Join("; ", offenders)}");
        Assert.Contains(rule.Trim(), offenders[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_spectrum_is_declared_in_its_part_alone_and_no_theme_remaps_it()
    {
        var declaring = Directory.GetFiles(Path.Combine(Assets.ProjectDir, "css-parts"), "*.css")
            .Where(p => Regex.IsMatch(Assets.StripComments(File.ReadAllText(p)), @"(?<![\w-])--spectrum-[a-z0-9-]+\s*:"))
            .Select(p => Path.GetFileName(p))
            .ToList();
        Assert.Equal([SpectrumPart], declaring);

        var remapped = Assets.TokenBlocks(Assets.StripComments(Assets.Css))
            .Where(b => b.Body.Contains(SpectrumPrefix, StringComparison.Ordinal))
            .Select(b => Assets.Squash(b.Selector))
            .ToList();
        Assert.True(remapped.Count == 0,
            "The spectrum is the same in every theme; these token blocks declare a spectrum value: "
            + string.Join(", ", remapped));
    }

    /// <summary>
    /// Every colour literal in a readable stylesheet outside the token blocks, the forced-colours
    /// system keywords and the spectrum's own declarations.
    /// </summary>
    private static List<string> Offenders(string readable)
    {
        // Which part each line of the bundle came from, read off the markers before the
        // comments that carry them are blanked.
        var partOf = new Dictionary<int, string>();
        var current = "";
        var number = 0;
        foreach (var raw in readable.Split('\n'))
        {
            number++;
            var marker = PartMarker.Match(raw);
            if (marker.Success) current = marker.Groups["part"].Value;
            partOf[number] = current;
        }

        var css = Assets.StripComments(readable);

        // Inside a forced-colors block the system palette IS the token layer: the
        // browser has already replaced every colour with the user's choice, and
        // `CanvasText` is the name of one of them. A focus outline there cannot go
        // through a var() — box-shadow is not painted in that mode, so the ring has to
        // be a real outline in a real system colour. Everywhere else the keywords stay
        // banned, which is what the line-range check preserves.
        var forcedColourLines = ForcedColourLineNumbers(css);

        var offenders = new List<string>();
        foreach (var (line, at) in Assets.LinesOutsideTokenBlocks(css))
        {
            string? kind = null;
            if (HexColour.IsMatch(line)) kind = "hex";
            else if (ColourFunction.IsMatch(line)) kind = "colour function";
            else if (NamedColour.IsMatch(line)) kind = "named colour";
            else if (SystemColour.IsMatch(line) && !forcedColourLines.Contains(at)) kind = "system colour";
            if (kind is null) continue;

            // The spectrum's own declarations, and nothing else: a system colour is no part
            // of a spectrum, and the line has to declare a --spectrum-* property in its part.
            var part = partOf.GetValueOrDefault(at, "");
            if (kind != "system colour" && part == SpectrumPart && SpectrumDeclaration.IsMatch(line)) continue;

            offenders.Add($"{part} (line {at}): {kind} — {line.Trim()}");
        }
        return offenders;
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
