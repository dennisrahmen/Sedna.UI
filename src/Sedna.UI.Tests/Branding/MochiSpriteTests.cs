using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Guards on <c>StateArt/Sedna.UI.mochi.svg</c>, Mochi's drawing.
/// </summary>
/// <remarks>
/// The same delivery as the state illustrations — embedded, and written into the page by
/// <c>SednaMochi</c> — so the same failures reach an app the same silent way: a sprite that
/// does not parse draws nothing, and a part with no rule draws in the initial black.
/// </remarks>
public class MochiSpriteTests
{
    private static readonly string SpritePath =
        Path.Combine(Assets.ProjectDir, "StateArt", "Sedna.UI.mochi.svg");

    private static readonly string StylePath =
        Path.Combine(Assets.ProjectDir, "css-parts", "68-mochi.css");

    private static string Source => File.ReadAllText(SpritePath);

    private static IEnumerable<string> SpriteClasses() =>
        Regex.Matches(Source, "class=\"([^\"]+)\"")
            .SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal);

    private static ISet<string> StyledParts() =>
        Regex.Matches(Assets.StripComments(File.ReadAllText(StylePath)), @"\.(sedna-mochi-[a-z0-9-]+)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void The_sprite_is_well_formed_xml()
    {
        var ex = Record.Exception(() => XDocument.Parse(Source));

        Assert.True(ex is null, $"{Path.GetFileName(SpritePath)} is not well-formed XML: {ex?.Message}");
    }

    [Fact]
    public void No_comment_contains_a_double_hyphen()
    {
        // A custom property written out in a comment ends the comment early, and the
        // whole sprite stops parsing. Name the property in 68-mochi.css instead.
        var offenders = Regex.Matches(Source, "<!--(.*?)-->", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .Where(body => body.Contains("--", StringComparison.Ordinal))
            .Select(body => body.Trim().Split('\n')[0])
            .ToList();

        Assert.True(offenders.Count == 0,
            "An XML comment may not contain two hyphens in a row: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void The_sprite_writes_no_colour()
    {
        // Every colour is a Mochi token through a class, so a tone, a theme override and
        // forced colours all reach the drawing.
        var colour = new Regex(
            @"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(|""(?:red|blue|green|black|white|gray|grey|orange|pink)""");

        var hits = colour.Matches(Source).Select(m => m.Value).Distinct().ToList();

        Assert.True(hits.Count == 0, "Found a colour in the sprite: " + string.Join(", ", hits));
    }

    [Fact]
    public void The_sprite_carries_no_style_and_only_library_names()
    {
        // In the page, the sprite's classes and ids share a namespace with the app's.
        Assert.DoesNotContain("<style", Source, StringComparison.Ordinal);
        Assert.DoesNotContain(" style=", Source, StringComparison.Ordinal);

        var classes = SpriteClasses().Where(c => !c.StartsWith("sedna-", StringComparison.Ordinal)).ToList();
        Assert.True(classes.Count == 0, "A class in the sprite is not a sedna-* name: " + string.Join(", ", classes));

        var ids = Regex.Matches(Source, "\\bid=\"([^\"]+)\"").Select(m => m.Groups[1].Value)
            .Where(id => !id.StartsWith("sedna-mochi", StringComparison.Ordinal)).ToList();
        Assert.True(ids.Count == 0, "An id in the sprite is not a sedna-mochi* name: " + string.Join(", ", ids));
    }

    [Fact]
    public void Every_part_has_a_rule_and_every_rule_has_a_part()
    {
        // The parts are the sprite's half of a contract whose other half is 68-mochi.css.
        // A part with no rule paints in black, or never shows; a rule with no part is a
        // pose that silently does nothing. CoverageTests skips these names, because an app
        // never writes one — this is where they are held instead.
        var parts = SpriteClasses().Where(c => c.StartsWith("sedna-mochi-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var styled = StyledParts();

        var unstyled = parts.Except(styled).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var orphaned = styled.Except(parts).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unstyled.Count == 0, "Parts in the sprite with no rule in 68-mochi.css: " + string.Join(", ", unstyled));
        Assert.True(orphaned.Count == 0, "Rules in 68-mochi.css for no part of the sprite: " + string.Join(", ", orphaned));
    }

    [Fact]
    public void The_component_renders_the_sprite_verbatim()
    {
        Assert.Equal(Source.Replace("\r\n", "\n", StringComparison.Ordinal),
            SednaMochi.Sprite.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void The_sprite_holds_one_drawing_on_the_documented_grid()
    {
        // 68-mochi.css and every example reference #sedna-mochi on a 160-unit grid; every
        // perch fraction is a line of that grid.
        var symbols = XDocument.Parse(Source).Descendants().Where(e => e.Name.LocalName == "symbol").ToList();

        var symbol = Assert.Single(symbols);
        Assert.Equal("sedna-mochi", (string?)symbol.Attribute("id"));
        Assert.Equal("0 0 160 160", (string?)symbol.Attribute("viewBox"));
    }
}
