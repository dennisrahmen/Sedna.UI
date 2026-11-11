using System.Globalization;

namespace Sedna.UI.Catalogue.Theming;

/// <summary>One variant's measurement of a contrast check.</summary>
/// <param name="Foreground">The colour on top, as painted — a translucent one already laid over its ground.</param>
/// <param name="Background">The colour under it, as painted.</param>
/// <param name="Ratio">The WCAG contrast ratio, or null when one of the two is not a colour the builder can read.</param>
internal sealed record PairReading(Rgba? Foreground, Rgba? Background, double? Ratio)
{
    public string RatioText => Ratio is { } r ? r.ToString("0.00", CultureInfo.InvariantCulture) + ":1" : "not measured";
}

/// <summary>One pair of colours the library paints on top of each other, measured in both variants.</summary>
/// <param name="Title">What the reader sees, in their words.</param>
/// <param name="Rule">Why it matters and what it has to reach, in a sentence.</param>
/// <param name="Foreground">The token on top.</param>
/// <param name="Background">The token under it.</param>
/// <param name="Floor">The ratio it has to reach.</param>
/// <param name="Fix">What to change when it falls short.</param>
internal sealed record ContrastCheck(
    string Title, string Rule, string Foreground, string Background, double Floor, string Fix,
    PairReading Dark, PairReading Light)
{
    public bool PassesIn(PairReading reading) => reading.Ratio is not { } r || r >= Floor;

    public bool Passes => PassesIn(Dark) && PassesIn(Light);

    public string FloorText => Floor.ToString("0.0", CultureInfo.InvariantCulture) + ":1";
}

/// <summary>How far the brand's hue is from one semantic family's — <c>docs/BRANDING.md</c> §3.3.</summary>
internal sealed record HueCheck(string Family, string FamilyToken, Rgba Brand, Rgba Other, double? Distance, double Floor, string Fix)
{
    public bool Passes => Distance is not { } d || d >= Floor;

    public string DistanceText => Distance is { } d ? d.ToString("0", CultureInfo.InvariantCulture) + "°" : "no hue";
}

/// <summary>
/// The pairs a theme can break, measured on what the generated <c>brand.css</c> produces.
/// </summary>
/// <remarks>
/// Each one is a pair the library's own tests hold its default theme to — <c>TokenTierTests</c>,
/// <c>SolidToneContrastTests</c>, <c>TintedTextContrastTests</c>, <c>ButtonEdgeContrastTests</c>,
/// <c>StateContrastTests</c>, <c>SurfaceTokenTests</c> — so every check passes until an edit
/// breaks it. The contrast arithmetic is <see cref="Colour.Contrast(Rgba, Rgba)"/>, the one this
/// project has.
/// </remarks>
internal static class ThemeChecks
{
    public const double Text = 4.5;
    public const double Edge = 3.0;

    private const string MoveApart = "Move one of the two further from the other in lightness: lighter text on a dark ground, darker on a light one.";

    private sealed record Definition(
        string Title, string Rule, string Foreground, string Background, double Floor, string Fix,
        string? AlternativeForeground = null, string Under = "--bg");

    private static readonly Definition[] Definitions =
    [
        new("Body text on the page", "Running text has to be readable by someone with low vision: at least 4.5:1 (WCAG AA).",
            "--fg", "--bg", Text, MoveApart),
        new("Body text on a card", "The same rule on a card, which is a step lighter than the page in dark.",
            "--fg", "--card-bg", Text, MoveApart),
        new("Quiet text on the page", "Hints, timestamps and a table's secondary column are still text: 4.5:1.",
            "--muted", "--bg", Text, MoveApart),
        new("Quiet text on a card", "The same quiet text on a card.",
            "--muted", "--card-bg", Text, MoveApart),
        new("Placeholder in an empty field", "The words in an empty field are read, so they are text: 4.5:1 on the field.",
            "--placeholder", "--bg", Text, "Point --placeholder at --muted, or at a step further from the field's background."),
        new("Disabled label", "A disabled control is still read, to know what it is: 4.5:1 on its own fill.",
            "--disabled-fg", "--disabled-bg", Text, "Take --disabled-fg a step further from --disabled-bg.", Under: "--card-bg"),
        new("Brand-coloured text on the page", "Text in the brand colour, a link-like label or a selected tab's name: 4.5:1.",
            "--brand-text", "--bg", Text, "Take --brand-text a ramp step further from the canvas: lighter in dark, darker in light."),
        new("Links on the page", "Links are drawn in the accent: 4.5:1 against the page.",
            "--accent", "--bg", Text, "Choose a lighter accent for dark and a darker one for light."),
        new("Links on a card", "The same links on a card.",
            "--accent", "--card-bg", Text, "Choose a lighter accent for dark and a darker one for light."),

        new("Label on a filled brand button", "The primary button's label sits on the brand colour itself: 4.5:1.",
            "--on-solid", "--brand", Text, "Choose a darker or more saturated brand colour: --brand is the step white text has to read on."),
        new("Label on a filled go button", "A go (sends outward) button filled: its label on the fill, 4.5:1.",
            "--on-solid", "--go-solid", Text, "Take --go-solid a step darker."),
        new("Label on a filled warn button", "A warn (control changes) button filled: its label on the fill, 4.5:1.",
            "--on-solid", "--warn-solid", Text, "Take --warn-solid a step darker."),
        new("Label on a filled danger button", "A danger button filled, the confirm in a destructive modal: 4.5:1.",
            "--on-solid", "--danger-solid", Text, "Take --danger-solid a step darker."),
        new("Label on a filled info badge", "The info family's solid has a label colour of its own: 4.5:1.",
            "--info-on-solid", "--info-solid", Text, "Move --info-on-solid or --info-solid apart."),
        new("Label on a filled secret badge", "Secret fills with its own text colour and labels it in the page colour: 4.5:1.",
            "--bg", "--secret-fg", Text, "Move --secret-fg further from --bg."),

        new("Brand text on a brand tint", "A brand badge or alert: brand text on its translucent tint over the page, 4.5:1.",
            "--brand-text", "--brand-tint", Text, "Take --brand-text further from the canvas, or lower --brand-tint's alpha."),
        new("Go text on its tint", "A go badge, alert or row: its text on its tint over the page, 4.5:1.",
            "--go-fg", "--go-bg", Text, "Take --go-fg further from the canvas, or lower --go-bg's alpha."),
        new("Warn text on its tint", "A warn badge, alert or row: its text on its tint over the page, 4.5:1.",
            "--warn-fg", "--warn-bg", Text, "Take --warn-fg further from the canvas, or lower --warn-bg's alpha."),
        new("Danger text on its tint", "A danger badge, alert or row: its text on its tint over the page, 4.5:1.",
            "--danger-fg", "--danger-bg", Text, "Take --danger-fg further from the canvas, or lower --danger-bg's alpha."),
        new("Info text on its tint", "An info badge, alert or row: its text on its tint over the page, 4.5:1.",
            "--info-fg", "--info-bg", Text, "Take --info-fg further from the canvas, or lower --info-bg's alpha."),
        new("Secret text on its tint", "A secret badge or alert: its text on its tint over the page, 4.5:1.",
            "--secret-fg", "--secret-bg", Text, "Take --secret-fg further from the canvas, or lower --secret-bg's alpha."),

        new("A filled brand button's boundary on a card", "Not text, so 3:1 — but the button has to be seen as a button: its fill or its edge, whichever stands out more.",
            "--brand-edge", "--card-bg", Edge, "Point --brand-edge at a lighter brand step in dark, or a darker one in light.",
            AlternativeForeground: "--brand"),
        new("Focus ring on the page", "The ring a focused control draws is how a keyboard user knows where they are: 3:1.",
            "--focus-ring", "--bg", Edge, "Point --focus-ring at a brand step further from the canvas."),
        new("Focus ring on a card", "The same ring on a card.",
            "--focus-ring", "--card-bg", Edge, "Point --focus-ring at a brand step further from the card."),
        new("Checkbox, radio and switch edge", "The edge is all that shows an unchecked box is there: 3:1.",
            "--control-border", "--card-bg", Edge, "Take --control-border a step further from the card."),
    ];

    private static readonly (string Family, string Token)[] Families =
    [
        ("danger", "--danger-solid"), ("go", "--go-solid"), ("warn", "--warn-solid"), ("info and links", "--info-solid"),
    ];

    /// <summary>The hue distance below which a brand reads as a semantic family.</summary>
    public const double HueFloor = 15;

    public static List<ContrastCheck> Contrast(
        IReadOnlyDictionary<string, Rgba> dark, IReadOnlyDictionary<string, Rgba> light) =>
        Definitions.Select(d => new ContrastCheck(d.Title, d.Rule, d.Foreground, d.Background, d.Floor, d.Fix,
            Read(d, dark), Read(d, light))).ToList();

    public static List<HueCheck> Hue(IReadOnlyDictionary<string, Rgba> dark)
    {
        var checks = new List<HueCheck>();
        if (!dark.TryGetValue("--brand", out var brand)) return checks;
        var brandHue = Colour.Hue(brand);

        foreach (var (family, token) in Families)
        {
            if (!dark.TryGetValue(token, out var other)) continue;
            var otherHue = Colour.Hue(other);
            double? distance = brandHue is { } a && otherHue is { } b ? Colour.HueDistance(a, b) : null;
            checks.Add(new HueCheck(family, token, brand, other, distance, HueFloor,
                $"Choose a brand further from {family}'s hue, or move the {family} family's ramp, as docs/BRANDING.md §3.3 says."));
        }

        return checks;
    }

    private static PairReading Read(Definition d, IReadOnlyDictionary<string, Rgba> colours)
    {
        if (!colours.TryGetValue(d.Under, out var under)) return new PairReading(null, null, null);
        var canvas = under.Over(new Rgba(0, 0, 0));
        if (!colours.TryGetValue(d.Background, out var bgRaw) || !colours.TryGetValue(d.Foreground, out var fgRaw))
            return new PairReading(null, null, null);

        var background = bgRaw.Over(canvas);
        var foreground = fgRaw.Over(background);
        var ratio = Colour.Contrast(foreground, background);

        if (d.AlternativeForeground is { } alternative && colours.TryGetValue(alternative, out var altRaw))
        {
            var other = altRaw.Over(background);
            var otherRatio = Colour.Contrast(other, background);
            if (otherRatio > ratio) (foreground, ratio) = (other, otherRatio);
        }

        return new PairReading(foreground, background, ratio);
    }
}
