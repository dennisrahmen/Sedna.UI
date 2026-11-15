using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A day nobody works is shaded so that it can be seen: the weekend and holiday shade stands apart
/// from the surface under it in every theme and palette.
/// </summary>
/// <remarks>
/// The shade was <c>--surface-soft</c>, 3 % white on the dark card, which measured about 1.06:1 and read as
/// no shade at all. It is a calendar token now, a step of the text colour, and this holds it apart.
/// </remarks>
public class CalendarShadeTests
{
    /// <summary>The least a shade has to stand apart from its ground to be seen at a glance.</summary>
    private const double Floor = 1.1;

    private static readonly string[] Grounds = ["--bg", "--card-bg"];

    [Theory]
    [MemberData(nameof(TintedTextContrastTests.ThemesAndPalettes), MemberType = typeof(TintedTextContrastTests))]
    public void A_weekend_and_a_holiday_stand_apart_from_their_ground(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();
        foreach (var shade in new[] { "--cal-weekend-bg", "--cal-holiday-bg" })
            foreach (var ground in Grounds)
            {
                var g = Tokens.Rgb(tokens, ground);
                var ratio = Tokens.Contrast(Tokens.Over(tokens, shade, g), g);
                if (ratio < Floor) failures.Add($"{shade} on {ground}: {ratio:0.000}:1");
            }
        Assert.True(failures.Count == 0, $"\"{theme.Name}\", {palette}: a shade under {Floor}:1 is not seen.\n  " + string.Join("\n  ", failures));
    }
}
