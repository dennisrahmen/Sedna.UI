using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Guards on <c>wwwroot/img/Sedna.UI.states.svg</c>, the state illustrations.
/// </summary>
/// <remarks>
/// The sprite is a shipped asset that nothing in the build parses, so every one of
/// these failures would otherwise reach a consuming app as a drawing that silently
/// does not appear or does not follow the theme.
/// </remarks>
public class StateSpriteTests
{
    private static readonly string SpritePath =
        Path.Combine(Assets.ProjectDir, "wwwroot", "img", "Sedna.UI.states.svg");

    private static string Source => File.ReadAllText(SpritePath);

    /// <summary>Every symbol in the sprite, in document order.</summary>
    public static TheoryData<string> SymbolIds()
    {
        var data = new TheoryData<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(SpritePath), "<symbol id=\"([a-z-]+)\""))
            data.Add(m.Groups[1].Value);
        return data;
    }

    [Fact]
    public void The_sprite_is_well_formed_xml()
    {
        // An SVG served as image/svg+xml is parsed by the XML parser, not the HTML
        // one, so a stray `--` inside a comment is not a lint warning: the whole file
        // fails to parse and every drawing in it disappears at once. That has already
        // happened here, which is why this test is the first one.
        var ex = Record.Exception(() => XDocument.Parse(Source));

        Assert.True(ex is null, $"{Path.GetFileName(SpritePath)} is not well-formed XML: {ex?.Message}");
    }

    [Fact]
    public void No_comment_contains_a_double_hyphen()
    {
        // The same failure, named rather than left to the parser, because the fix is
        // not obvious from "invalid token": a CSS custom property or a BEM modifier
        // written out in a comment is what breaks it.
        var offenders = Regex.Matches(Source, "<!--(.*?)-->", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .Where(body => body.Contains("--", StringComparison.Ordinal))
            .Select(body => body.Trim().Split('\n')[0])
            .ToList();

        Assert.True(offenders.Count == 0,
            "An XML comment may not contain two hyphens in a row. Name the property in "
            + "42-state-art.css instead of writing it out here: "
            + string.Join(" | ", offenders));
    }

    [Fact]
    public void The_sprite_writes_no_colour()
    {
        // The whole point of the sprite is that it follows the theme without being
        // told which one is on. A single hex here is a drawing that stays the same
        // colour in light, colour-blind and forced-colours mode, and nothing else in
        // the library would look wrong beside it — so nobody would notice.
        var colour = new Regex(
            @"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(|""(?:red|blue|green|black|white|gray|grey|orange)""");

        var hits = colour.Matches(Source).Select(m => m.Value).Distinct().ToList();

        Assert.True(hits.Count == 0,
            "Line work is currentColor and the accent is var(--state-accent). Found: "
            + string.Join(", ", hits));
    }

    [Theory]
    [MemberData(nameof(SymbolIds))]
    public void Every_symbol_carries_the_shared_viewBox(string id)
    {
        // One grid for the whole set. A symbol on a different viewBox renders at a
        // different apparent weight beside its siblings at the same CSS size, and the
        // set stops reading as one set.
        var symbol = XDocument.Parse(Source)
            .Descendants()
            .Single(e => e.Name.LocalName == "symbol" && (string?)e.Attribute("id") == id);

        Assert.Equal("0 0 120 120", (string?)symbol.Attribute("viewBox"));
    }

    [Fact]
    public void The_sprite_holds_the_states_the_stylesheet_documents()
    {
        // 42-state-art.css lists the ids as the public surface of this file: an app
        // reads that list and writes one of them into a `use`. A drawing renamed here
        // and not there is a `use` that resolves to nothing and renders an empty box.
        var css = File.ReadAllText(
            Path.Combine(Assets.ProjectDir, "css-parts", "42-state-art.css"));

        var documented = Regex.Match(css, @"Thirteen symbols:(.*?)\.\s", RegexOptions.Singleline)
            .Groups[1].Value
            .Split(',', '\n')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var actual = Regex.Matches(Source, "<symbol id=\"([a-z-]+)\"")
            .Select(m => m.Groups[1].Value)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(actual, documented);
    }
}
