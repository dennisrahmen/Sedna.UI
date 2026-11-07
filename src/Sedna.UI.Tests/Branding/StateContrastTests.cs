using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The tokens every interactive control draws its state with clear WCAG on every surface, in every
/// theme and palette: the focus ring, a check's, radio's or switch's edge and a rejected or unusual
/// field's edge at 3:1 (1.4.11), and a disabled control's label at 4.5:1 on its own fill.
/// </summary>
/// <remarks>
/// A disabled control is exempt from 1.4.3, and this library does not take the exemption: the label
/// is how a reader learns what they cannot do yet. These are tokens because every control reads
/// them, so measuring the token measures every control.
/// </remarks>
public class StateContrastTests
{
    private static readonly string[] Surfaces = ["--bg", "--card-bg", "--bg-elevated"];

    [Theory]
    [MemberData(nameof(TintedTextContrastTests.ThemesAndPalettes), MemberType = typeof(TintedTextContrastTests))]
    public void Focus_ring_control_edge_and_disabled_label_clear_WCAG(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();

        foreach (var surface in Surfaces)
        {
            var ground = Tokens.Rgb(tokens, surface);
            Check("--focus-ring", Tokens.Over(tokens, "--focus-ring", ground), ground, 3.0, surface);
            Check("--control-border", Tokens.Over(tokens, "--control-border", ground), ground, 3.0, surface);
            // A field in a state is still a field: its edge is the only boundary it has.
            Check("--field-border-danger", Tokens.Over(tokens, "--field-border-danger", ground), ground, 3.0, surface);
            Check("--field-border-warn", Tokens.Over(tokens, "--field-border-warn", ground), ground, 3.0, surface);

            var fill = Tokens.Over(tokens, "--disabled-bg", ground);
            Check("--disabled-fg on --disabled-bg", Tokens.Rgb(tokens, "--disabled-fg"), fill, 4.5, surface);
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}:\n  " + string.Join("\n  ", failures));

        void Check(string what, (double R, double G, double B) fg, (double R, double G, double B) bg, double floor, string surface)
        {
            var ratio = Tokens.Contrast(fg, bg);
            if (ratio < floor) failures.Add($"{what} over {surface} is {ratio:0.000}:1, under {floor}:1");
        }
    }
}
