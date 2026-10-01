using System.Globalization;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Mochi's rig, read from <c>68-mochi.css</c> and the sprite: the vocabulary an app
/// writes, the angles the joints ease between, and the lines the perches place.
/// </summary>
/// <remarks>
/// A pose change is a CSS transition between two angles, and an angle interpolates
/// numerically — so a wave at −53° after an idle at 67° swings forward through 7°, but
/// the same wave written as 307° would swing all the way round through the body. The
/// ranges below are what make every change between any two poses the short, natural
/// way round, and they hold for every value in the file, not just the ones somebody
/// thought to try.
/// </remarks>
public class MochiRigTests
{
    private static readonly string Css =
        Assets.StripComments(File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", "68-mochi.css")));

    private static readonly string Header =
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", "68-mochi.css"));

    private static readonly string Sprite =
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "StateArt", "Sedna.UI.mochi.svg"));

    /// <summary>The value each attribute means when it is absent, which needs no rule.</summary>
    private static readonly Dictionary<string, string> Defaults = new(StringComparer.Ordinal)
    {
        ["action"] = "idle",
        ["mood"] = "neutral",
        ["tone"] = "pink",
    };

    /// <summary>Each arm's range of angles, in degrees, as the stylesheet header states them.</summary>
    private static readonly Dictionary<string, (double Min, double Max)> Ranges = new(StringComparer.Ordinal)
    {
        ["bl"] = (90, 280),
        ["br"] = (-100, 90),
        ["fl"] = (0, 90),
        ["fr"] = (-45, 190),
    };

    private static List<string> Documented(string heading) =>
        Regex.Match(Header, heading + @":\s*(.*?)\.\s*$", RegexOptions.Singleline | RegexOptions.Multiline)
            .Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    [Theory]
    [InlineData("Actions", "action")]
    [InlineData("Moods", "mood")]
    [InlineData("Tones", "tone")]
    [InlineData("Perches", "perch")]
    public void The_documented_vocabulary_is_the_styled_one(string heading, string attribute)
    {
        // The header lists what an app may write; the rules are what does something.
        // A value in one and not the other is either a pose nobody can find or an
        // attribute that silently does nothing.
        var documented = Documented(heading);
        Assert.NotEmpty(documented);

        var styled = Regex.Matches(Css, $@"\[data-{attribute}=""([a-z]+)""\]")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var expected = documented.Where(v => !Defaults.TryGetValue(attribute, out var d) || v != d)
            .OrderBy(v => v, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, styled.OrderBy(v => v, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Every_arm_angle_lies_in_its_arm_s_range()
    {
        var offenders = new List<string>();
        var count = 0;
        foreach (Match m in Regex.Matches(Css, @"--(bl|br|fl|fr)-a:\s*(-?[\d.]+)deg"))
        {
            count++;
            var arm = m.Groups[1].Value;
            var angle = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var (min, max) = Ranges[arm];
            if (angle < min || angle > max) offenders.Add($"--{arm}-a: {angle}deg is outside {min}..{max}");
        }

        Assert.True(count > 0, "No arm angles found; the property names changed.");
        Assert.True(offenders.Count == 0,
            "An angle outside its arm's range makes a pose change swing the long way round, through "
            + "the body: " + string.Join("; ", offenders));
    }

    [Fact]
    public void The_header_states_the_ranges_the_test_holds()
    {
        // Two copies of four ranges: the one a reader of the stylesheet sees, and this one.
        foreach (var (arm, (min, max)) in Ranges)
        {
            var name = arm switch { "bl" => "the left arm", "br" => "the right", "fl" => "the front left", _ => "the front right" };
            Assert.Contains($"{name} {min:0}°–{max:0}°".Replace("-", "−", StringComparison.Ordinal), Header, StringComparison.Ordinal);
        }
    }

    /// <summary>A line of the drawing, as the sprite's own header states it.</summary>
    private static double Line(string what)
    {
        var m = Regex.Match(Sprite, what + @" at (x|y) (\d+(?:\.\d+)?)");
        Assert.True(m.Success, $"The sprite's header no longer states {what}.");
        return double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>The fraction of --size a perch rule multiplies by.</summary>
    private static double Fraction(string perch, string property)
    {
        var rule = Regex.Match(Css, $@"\.mochi\[data-perch=""{perch}""\](?:,\s*\.mochi\[data-perch=""[a-z]+""\])*\s*\{{(?<body>[^}}]*)\}}");
        Assert.True(rule.Success, $"No rule for the {perch} perch.");
        var m = Regex.Match(rule.Groups["body"].Value, property + @":\s*calc\([^;]*?var\(--size\)\s*\*\s*(-?[\d.]+)\)");
        Assert.True(m.Success, $"The {perch} perch sets no {property} from --size.");
        return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Each_perch_places_the_line_the_sprite_documents()
    {
        // The perch puts one line of the 160-unit drawing on one edge of the element. If
        // the drawing moves and the fraction does not, Mochi floats above its seat or sinks
        // into the card, by the same proportion at every size.
        const double grid = 160;
        Assert.Equal(-Line("the seat") / grid, Fraction("sit", "inset-block-start"), 4);
        Assert.Equal(-Line("the peeking edge") / grid, Fraction("peek", "inset-block-start"), 4);
        Assert.Equal(-(grid - Line("the feet")) / grid, Fraction("stand", "inset-block-end"), 4);
        Assert.Equal(Line("the hanging grip") / grid, Fraction("hang", "inset-block-start"), 4);
        Assert.Equal(Line("the side edge") / grid, Fraction("side", "inset-inline-start"), 4);
    }

    [Theory]
    [InlineData("peek", 2, 104)]
    [InlineData("side", 3, 60)]
    [InlineData("hang", 0, 30)]
    public void What_hides_behind_the_element_is_clipped_on_the_perch_line(string perch, int side, double line)
    {
        // `inset(top right bottom left) view-box`, in the drawing's own units. The clip
        // may overlap the line by a hair, under the hands that cover the seam, but never
        // stop short of it: a gap would show the body through the card.
        var rule = Regex.Match(Css, $@"\.mochi\[data-perch=""{perch}""\][^{{]*\{{(?<body>[^}}]*)\}}").Groups["body"].Value;
        var clip = Regex.Match(rule, @"--clip:\s*inset\(([^)]*)\)\s*view-box");
        Assert.True(clip.Success, $"The {perch} perch clips nothing.");

        var insets = clip.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v.Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture))
            .ToArray();
        var edge = side switch { 0 => insets[0], 1 => 160 - insets[1], 2 => 160 - insets[2], _ => insets[3] };

        Assert.InRange(Math.Abs(edge - line), 0, 2);
    }

    [Theory]
    [InlineData("sedna-mochi-grip-l", "the peeking edge", "cy")]
    [InlineData("sedna-mochi-grip-r", "the peeking edge", "cy")]
    [InlineData("sedna-mochi-grip-hl", "the hanging grip", "cy")]
    [InlineData("sedna-mochi-grip-hr", "the hanging grip", "cy")]
    [InlineData("sedna-mochi-grip-side", "the side edge", "cx")]
    public void The_hands_that_hold_an_edge_are_on_it(string grip, string line, string axis)
    {
        var group = Regex.Match(Sprite, $@"<g class=""{grip}"">(.*?)</g>", RegexOptions.Singleline);
        Assert.True(group.Success, $"No {grip} in the sprite.");
        var palm = Regex.Match(group.Groups[1].Value, $@"class=""sedna-mochi-palm""[^>]*\b{axis}=""([\d.]+)""");
        Assert.True(palm.Success, $"{grip} has no palm.");

        var at = double.Parse(palm.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.InRange(Math.Abs(at - Line(line)), 0, 1.5);
    }
}
