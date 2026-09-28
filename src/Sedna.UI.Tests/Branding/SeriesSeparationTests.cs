using System.Globalization;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The six series colours stay told apart: every pair, in both variants of the shipped
/// palette, for full colour vision and under protanopia and deuteranopia.
/// </summary>
/// <remarks>
/// <para>
/// A series colour has one job, which is being different from the other five. The palette's
/// support ramps share one lightness curve, so two hues at the same step differ by hue alone —
/// and cyan and teal, and violet and indigo, are close hues. At one step each, a graph's
/// <c>data-tone="2"</c> and <c>"4"</c> were one colour at a glance. Every pair is measured, not
/// only neighbours, because a graph or a legend puts any two side by side.
/// </para>
/// <para>
/// Distance is Euclidean in OKLab ×100. 15 is the floor for full colour vision. 8 is the floor
/// under the Machado, Oliveira and Fernandes (2009) simulation at severity 1.0 — the model the
/// figure is calibrated against, so replacing the model means re-deriving the floor.
/// </para>
/// <para>
/// A theme is not measured here. It replaces <c>--viz-1</c>'s hue with its brand, and a brand
/// that lands on a series hue is the theme's collision to move, the way
/// <see cref="SednaThemeCollisionTests"/> makes it move a semantic family.
/// </para>
/// </remarks>
public class SeriesSeparationTests
{
    private const double FullColourFloor = 15;
    private const double ColourBlindFloor = 8;

    private static readonly string[] Series = ["--viz-1", "--viz-2", "--viz-3", "--viz-4", "--viz-5", "--viz-6"];

    /// <summary>Machado, Oliveira and Fernandes (2009), severity 1.0, on linear sRGB.</summary>
    private static readonly (string Name, double[,] Matrix)[] Simulations =
    [
        ("protanopia", new[,]
        {
            { 0.152286, 1.052583, -0.204868 },
            { 0.114503, 0.786281, 0.099216 },
            { -0.003882, -0.048116, 1.051998 },
        }),
        ("deuteranopia", new[,]
        {
            { 0.367322, 0.860646, -0.227968 },
            { 0.280085, 0.672501, 0.047413 },
            { -0.011820, 0.042940, 0.968881 },
        }),
    ];

    [Theory]
    [InlineData(Tokens.Dark)]
    [InlineData(Tokens.Light)]
    public void Every_pair_of_series_colours_is_told_apart(string variant)
    {
        var tokens = Tokens.Resolved(variant);
        var colours = Series.Select(name => Tokens.Rgb(tokens, name)).ToArray();
        var failures = new List<string>();

        for (var i = 0; i < colours.Length; i++)
            for (var j = i + 1; j < colours.Length; j++)
            {
                var full = Distance(colours[i], colours[j], simulation: null);
                if (full < FullColourFloor)
                    failures.Add($"{Series[i]} / {Series[j]}: ΔE {full:0.0}, under {FullColourFloor}");

                foreach (var (name, matrix) in Simulations)
                {
                    var simulated = Distance(colours[i], colours[j], matrix);
                    if (simulated < ColourBlindFloor)
                        failures.Add($"{Series[i]} / {Series[j]}: ΔE {simulated:0.0} under {name}, under {ColourBlindFloor}");
                }
            }

        Assert.True(failures.Count == 0,
            $"{variant}: these series colours read as one colour.\n  "
            + string.Join("\n  ", failures)
            + "\nMove one of the pair to another step of its own ramp — a close pair separates "
            + "by lightness — and re-run; see the comment above --viz-1 in 01-tokens.css.");
    }

    /// <summary>OKLab distance ×100, optionally after simulating a colour-vision deficiency.</summary>
    private static double Distance(
        (double R, double G, double B) a, (double R, double G, double B) b, double[,]? simulation)
    {
        var (l1, a1, b1) = Oklab(a, simulation);
        var (l2, a2, b2) = Oklab(b, simulation);
        return 100 * Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    /// <summary>
    /// OKLab through <see cref="Oklch.FromHex"/>, so the conversion has one implementation. A
    /// simulated colour is re-encoded to 8-bit hex on the way, which moves a distance by well
    /// under 1 — immaterial next to the floors.
    /// </summary>
    private static (double L, double A, double B) Oklab((double R, double G, double B) srgb, double[,]? simulation)
    {
        double[] rgb = [srgb.R, srgb.G, srgb.B];

        if (simulation is not null)
        {
            double[] linear = [.. rgb.Select(ToLinear)];
            rgb = [.. Enumerable.Range(0, 3).Select(row => ToSrgb(
                simulation[row, 0] * linear[0] + simulation[row, 1] * linear[1] + simulation[row, 2] * linear[2]))];
        }

        var hex = "#" + string.Concat(rgb.Select(c =>
            ((int)Math.Round(Math.Clamp(c, 0, 1) * 255, MidpointRounding.AwayFromZero))
                .ToString("x2", CultureInfo.InvariantCulture)));

        var (l, c, h) = Oklch.FromHex(hex);
        var radians = h * Math.PI / 180;
        return (l, c * Math.Cos(radians), c * Math.Sin(radians));
    }

    private static double ToLinear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static double ToSrgb(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        return c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
    }
}
