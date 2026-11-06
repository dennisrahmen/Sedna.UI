using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every tone's filled emphasis — <c>.btn--solid</c>, <c>.badge--solid</c>, <c>.alert--solid</c> —
/// sets its label at AA on its fill, in every theme and palette.
/// </summary>
/// <remarks>
/// A tone whose fill is its own text colour flips with the theme: light in dark, dark in light. A
/// fixed white label on it read 1.6:1 on the dark theme's violet. The carriers say which label goes
/// on which fill, so every tone a carrier names is measured and none is listed here.
/// </remarks>
public class SolidToneContrastTests
{
    private const double Floor = 4.5;

    [Theory]
    [MemberData(nameof(TintedTextContrastTests.ThemesAndPalettes), MemberType = typeof(TintedTextContrastTests))]
    public void Every_tone_sets_its_solid_label_at_AA(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var carriers = Tones.Carriers();
        Assert.NotEmpty(carriers);

        var failures = new List<string>();
        foreach (var (tone, props) in carriers)
        {
            Assert.True(props.ContainsKey("solid") && props.ContainsKey("on-solid"),
                $"The {tone} tone names no --tone-solid or --tone-on-solid.");
            var ratio = Tokens.Contrast(tokens, props["on-solid"], props["solid"]);
            if (ratio < Floor)
                failures.Add($"{tone}: {props["on-solid"]} on {props["solid"]} is {ratio:0.000}:1");
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}: these solid tones read under {Floor}:1.\n  " + string.Join("\n  ", failures));
    }
}
