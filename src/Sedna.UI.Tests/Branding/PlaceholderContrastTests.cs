using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The quiet texts — a placeholder, an empty editor's prompt, the dash of a stat that could not be
/// read, a muted series, a disabled drag handle's grip — clear AA on every surface they sit on, in
/// every palette and built-in theme.
/// </summary>
/// <remarks>
/// <para>
/// They were painted with <c>--border-strong</c>, a border role held to 3:1, and measured 2.2–2.7:1:
/// the colour was chosen because it looked quiet, not because it was a text role. So the colour each
/// rule paints is read from the stylesheet rather than assumed, and resolved through each palette to
/// the literal it ends at — a rule that drifts back to a border token fails here by its measurement.
/// </para>
/// <para>
/// <c>Every_placeholder_in_the_sheet_is_measured</c> keeps the list honest: a new
/// <c>::placeholder</c> rule that is not listed fails until it is.
/// </para>
/// </remarks>
public class PlaceholderContrastTests
{
    private const double Floor = 4.5;

    /// <summary>The rules measured, each by the selector the stylesheet writes it under.</summary>
    private static readonly string[] Texts =
    [
        ".form-input::placeholder",
        ".form-combo-input::placeholder",
        ".palette-input::placeholder",
        ".search-input::placeholder",
        ".nav-filter-input::placeholder",
        ".editor .ql-editor.ql-blank::before",
        ".stat--unavailable .stat-value",
        ".series-muted",
        "[aria-disabled=\"true\"] > .drag-handle",
    ];

    /// <summary>
    /// Where they are read: each canvas, the elevated surface a palette or modal sits on, and the
    /// search box's own wash over the header.
    /// </summary>
    private static readonly (string Name, Func<Dictionary<string, string>, (double R, double G, double B)> Paint)[]
        Grounds =
        [
            ("--bg", t => Tokens.Rgb(t, "--bg")),
            ("--card-bg", t => Tokens.Rgb(t, "--card-bg")),
            ("--bg-elevated", t => Tokens.Rgb(t, "--bg-elevated")),
            ("--surface-strong on --bg-elevated", t => Tokens.Over(t, "--surface-strong", "--bg-elevated")),
        ];

    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    /// <summary>Each single-selector rule's <c>color</c> token, keyed by its squashed selector.</summary>
    private static Dictionary<string, string> ColourRules()
    {
        var css = Tokens.WithoutMediaBlocks(Assets.StripComments(Assets.Css));
        var rules = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match rule in Rule.Matches(css))
        {
            var colour = Regex.Match(rule.Groups["body"].Value,
                @"(?<![-\w])color\s*:\s*var\(\s*(?<t>--[a-z0-9-]+)\s*\)");
            if (!colour.Success) continue;

            foreach (var part in rule.Groups["selector"].Value.Split(','))
                rules[Assets.Squash(part)] = colour.Groups["t"].Value;
        }

        return rules;
    }

    [Fact]
    public void Every_listed_text_paints_through_a_token()
    {
        var rules = ColourRules();
        var missing = Texts.Where(t => !rules.ContainsKey(Assets.Squash(t))).ToList();

        Assert.True(missing.Count == 0,
            "These no longer set `color: var(--…)` under this selector, so nothing here measures them: "
            + string.Join(", ", missing) + ".");
    }

    [Fact]
    public void Every_placeholder_in_the_sheet_is_measured()
    {
        var listed = Texts.Select(Assets.Squash).ToHashSet(StringComparer.Ordinal);
        var unlisted = ColourRules().Keys
            .Where(s => s.Contains("::placeholder", StringComparison.Ordinal) && !listed.Contains(s))
            .ToList();

        Assert.True(unlisted.Count == 0,
            "These placeholders paint a colour and are not measured: " + string.Join(", ", unlisted)
            + ". Add them to Texts.");
    }

    [Fact]
    public void No_text_is_painted_in_a_border_role()
    {
        var rules = ColourRules();
        var offenders = Texts
            .Select(Assets.Squash)
            .Where(s => rules.TryGetValue(s, out var token) && token.StartsWith("--border", StringComparison.Ordinal))
            .Select(s => $"{s} → {rules[s]}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "A border role is held to 3:1, not to the 4.5:1 text needs. Use --placeholder or --muted:\n  "
            + string.Join("\n  ", offenders));
    }

    public static IEnumerable<object[]> ThemesAndPalettes() => TintedTextContrastTests.ThemesAndPalettes();

    [Theory]
    [MemberData(nameof(ThemesAndPalettes))]
    public void Every_quiet_text_reads_at_AA_where_it_is_set(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var rules = ColourRules();
        var failures = new List<string>();

        foreach (var text in Texts)
        {
            var token = rules[Assets.Squash(text)];
            foreach (var (ground, paint) in Grounds)
            {
                var ratio = Tokens.Contrast(Tokens.Rgb(tokens, token), paint(tokens));
                if (ratio < Floor)
                    failures.Add($"{text}: {token} on {ground} is {ratio:0.000}:1");
            }
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}: these read under {Floor}:1.\n  " + string.Join("\n  ", failures)
            + "\nPoint the rule at a text role (--placeholder, --muted), not a border step.");
    }
}
