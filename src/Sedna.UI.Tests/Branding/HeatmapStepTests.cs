using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A heatmap's five intensity steps stay five steps: every neighbouring pair is told apart, for
/// full colour vision and under protanopia and deuteranopia, in every palette a reader can pick
/// and in every built-in theme, and each step lies further from the card than the one before.
/// </summary>
/// <remarks>
/// <para>
/// The steps are an order, not a set of categories, so the pairs that matter are neighbours —
/// the empty cell and the first step most of all, because "nothing" and "a little" is the
/// distinction a contribution calendar is read for. The order itself is carried by lightness,
/// which no colour vision changes, so lightness has to move away from the card at every step.
/// </para>
/// <para>
/// Distance is <see cref="SeriesSeparationTests.Distance"/>, OKLab ×100. The floors are the
/// series floors' reasoning applied to an ordered ramp: 10 for full colour vision, where a
/// neighbour differs in lightness by design, and the series' 7.5 under simulation. Do not lower
/// either to fit a step — move the step along its ramp.
/// </para>
/// </remarks>
public class HeatmapStepTests
{
    private const double FullColourFloor = 10;
    private const double ColourBlindFloor = 7.5;
    private const double LightnessStep = 0.05;

    private static readonly string[] Steps = ["--heat-0", "--heat-1", "--heat-2", "--heat-3", "--heat-4"];

    public static IEnumerable<object[]> PalettesAndThemes() =>
        Tokens.Palettes.Select(p => new object[] { SednaTheme.Sedna.Name, p })
            .Concat(from row in SednaThemeTests.BuiltInThemes()
                    let theme = (SednaTheme)row[0]
                    where theme.Name != SednaTheme.Sedna.Name
                    from variant in new[] { Tokens.Dark, Tokens.Light }
                    select new object[] { theme.Name, variant });

    [Theory]
    [MemberData(nameof(PalettesAndThemes))]
    public void Neighbouring_steps_are_told_apart_and_ordered_away_from_the_card(string themeName, string palette)
    {
        var theme = SednaThemeTests.BuiltInThemes().Select(r => (SednaTheme)r[0]).Single(t => t.Name == themeName);
        var tokens = Tokens.Resolved(palette, theme.Name == SednaTheme.Sedna.Name ? null : theme.Palette);
        var card = Tokens.Rgb(tokens, "--card-bg");
        // Step 0 is the track, which is translucent in the default palettes: measure what it paints.
        var colours = Steps.Select(s => Tokens.Over(tokens, s, card)).ToArray();
        var failures = new List<string>();

        for (var i = 1; i < colours.Length; i++)
        {
            var full = SeriesSeparationTests.Distance(colours[i - 1], colours[i], simulation: null);
            if (full < FullColourFloor)
                failures.Add($"{Steps[i - 1]} / {Steps[i]}: ΔE {full:0.0}, under {FullColourFloor}");

            foreach (var (name, matrix) in SeriesSeparationTests.Simulations)
            {
                var simulated = SeriesSeparationTests.Distance(colours[i - 1], colours[i], matrix);
                if (simulated < ColourBlindFloor)
                    failures.Add($"{Steps[i - 1]} / {Steps[i]}: ΔE {simulated:0.0} under {name}, under {ColourBlindFloor}");
            }
        }

        var cardLightness = SeriesSeparationTests.Lightness(card);
        var away = colours.Select(c => Math.Abs(SeriesSeparationTests.Lightness(c) - cardLightness)).ToArray();
        for (var i = 1; i < away.Length; i++)
            if (away[i] - away[i - 1] < LightnessStep)
                failures.Add($"{Steps[i]} is {away[i]:0.00} from the card in lightness, under {LightnessStep} further than {Steps[i - 1]} ({away[i - 1]:0.00})");

        Assert.True(failures.Count == 0,
            $"\"{themeName}\", {palette}: the heatmap's steps do not read as five steps in order.\n  "
            + string.Join("\n  ", failures)
            + "\nMove a step along its ramp in this palette's token block — see the comment above --heat-0 in 01-tokens.css.");
    }
}
