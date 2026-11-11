using System.Text.Json;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Catalogue.Theming;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>/theme-builder</c>: the tokens it offers, the <c>brand.css</c> it generates, the checks it
/// measures, and the page that shows them.
/// </summary>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class ThemeBuilderTests(CatalogueAppFixture app)
{
    private static readonly Regex Declared = new(@"(?:^|[{;])\s*(--[\w-]+)\s*:", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Lazy<ThemeTokens> LazyLibrary =
        new(() => ThemeTokens.From(JsonDocument.Parse(File.ReadAllText(Assets.TokensPath))));

    private static ThemeTokens Library => LazyLibrary.Value;

    private static readonly Dictionary<string, string> None = new(StringComparer.Ordinal);

    private static HashSet<string> Names(string css) =>
        Declared.Matches(Assets.StripComments(css)).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static ThemeResult Build(
        QuickStart? start = null, Dictionary<string, string>? dark = null, Dictionary<string, string>? light = null) =>
        BrandCssBuilder.Build(Library, start ?? BrandCssBuilder.Ramps(BrandCssBuilder.SednaRed), dark ?? None, light ?? None);

    private static Dictionary<string, string> Set(params (string Name, string Value)[] values) =>
        values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> Block(string selector)
    {
        using var export = JsonDocument.Parse(File.ReadAllText(Assets.TokensPath));
        return export.RootElement.GetProperty("blocks").EnumerateArray()
            .Where(b => b.GetProperty("media").ValueKind == JsonValueKind.Null && b.GetProperty("selector").GetString() == selector)
            .SelectMany(b => b.GetProperty("tokens").EnumerateObject())
            .ToDictionary(t => t.Name, t => t.Value.GetString()!, StringComparer.Ordinal);
    }

    // ── The tokens it offers ───────────────────────────────────────────────

    [Fact]
    public void Every_token_at_root_is_offered_unless_the_device_or_the_app_sets_it()
    {
        var root = Block(":root");
        Assert.NotEmpty(root);

        foreach (var (name, value) in root)
        {
            var leftOut = value.Trim() == "initial" || value.Contains("env(", StringComparison.Ordinal);
            Assert.True(leftOut != Library.ByName.ContainsKey(name),
                leftOut ? $"{name} ({value}) is offered, but nothing an app writes sets it."
                        : $"{name} is declared at :root and the theme builder does not offer it.");
        }

        Assert.DoesNotContain("--level", Library.ByName.Keys);
        Assert.DoesNotContain("--safe-block-start", Library.ByName.Keys);
    }

    [Fact]
    public void Each_token_carries_the_librarys_dark_and_light_values()
    {
        var root = Block(":root");
        var light = Block(ThemeTokens.LightSelector);
        Assert.NotEmpty(light);

        foreach (var token in Library.ByName.Values)
        {
            Assert.Equal(root[token.Name], token.Dark);
            Assert.Equal(light.GetValueOrDefault(token.Name), token.Light);
        }

        Assert.Equal("var(--slate-900)", Library.ByName["--bg"].Dark);
        Assert.Equal("var(--slate-50)", Library.ByName["--bg"].Light);
    }

    [Fact]
    public void The_groups_are_the_token_pages_and_every_one_is_placed()
    {
        // TokenPageTests holds TokenGroups to the export, so nothing should fall through.
        Assert.DoesNotContain(Library.Groups, g => g.Name == ThemeTokens.Ungrouped);
        Assert.All(Library.Groups, g => Assert.Contains(TokenGroups.All, t => t.Name == g.Name));
        Assert.Equal(Library.ByName.Count, Library.Groups.Sum(g => g.Tokens.Count));
        Assert.Equal(Library.Groups.Count, Library.Groups.Select(g => g.Slug).Distinct().Count());

        // Sections in order: roles, then the sizes, then the palette.
        var sections = Library.Groups.Select(g => g.Section).Distinct().ToList();
        Assert.Equal(ThemeTokens.Sections, sections);
    }

    [Theory]
    [InlineData("--brand", "Colour", "Role")]
    [InlineData("--brand-tint", "Colour", "Role")]
    [InlineData("--coral-600", "Colour", "Palette")]
    [InlineData("--slate-900", "Colour", "Palette")]
    [InlineData("--radius-control", "Value", "Role")]
    [InlineData("--font-sans", "Value", "Role")]
    [InlineData("--motion-fast", "Value", "Role")]
    [InlineData("--shadow-modal", "Value", "Role")]
    [InlineData("--control-height", "Value", "Role")]
    public void A_token_is_offered_as_what_it_is(string name, string kind, string tier)
    {
        var token = Library.ByName[name];
        Assert.Equal(kind, token.Kind.ToString());
        Assert.Equal(tier, token.Tier.ToString());
    }

    [Fact]
    public void The_resolver_follows_the_stylesheets_own_forms()
    {
        // var() through the palette, and a color-mix with transparent keeps its alpha.
        Assert.Equal("#d73f1a", Library.DarkColours["--brand"].Hex);
        Assert.Equal("#0f172a", Library.DarkColours["--bg"].Hex);
        Assert.Equal("#f8fafc", Library.LightColours["--bg"].Hex);
        var divider = Library.DarkColours["--divider"];
        Assert.InRange(divider.A, 0.059, 0.061);

        var mixed = CssColour.Parse("color-mix(in srgb, #ff0000 50%, #0000ff)", _ => null)!.Value;
        Assert.Equal("#800080", mixed.Hex);
        Assert.Equal("#f4a6bc", CssColour.Parse("rgb(244, 166, 188)", _ => null)!.Value.Hex);
        Assert.Null(CssColour.Parse("currentColor", _ => null));
    }

    // ── What it generates ──────────────────────────────────────────────────

    [Fact]
    public void Nothing_changed_writes_no_declaration_and_every_check_passes()
    {
        var result = Build();

        Assert.Equal(0, result.Changed);
        Assert.Empty(Names(result.Css));
        Assert.NotEmpty(result.Checks);
        Assert.NotEmpty(result.HueChecks);
        Assert.All(result.Checks, c =>
        {
            Assert.True(c.Dark.Ratio is not null && c.Light.Ratio is not null, $"{c.Title} was not measured.");
            Assert.True(c.Passes, $"{c.Title}: {c.Dark.RatioText} dark, {c.Light.RatioText} light, needs {c.FloorText}");
        });
        Assert.All(result.HueChecks, c => Assert.True(c.Passes, $"Brand against {c.Family}: {c.DistanceText}"));
    }

    [Fact]
    public void Only_a_changed_token_is_written()
    {
        var result = Build(dark: Set(("--radius-control", "10px")));

        Assert.Equal(["--radius-control"], Names(result.Css));
        Assert.Contains("--radius-control: 10px;", result.Css, StringComparison.Ordinal);
        Assert.Equal(1, result.Changed);

        // The library's own value, however it is spaced, is no change.
        Assert.Empty(Names(Build(dark: Set(("--radius-control", " 5px "))).Css));
        Assert.Empty(Names(Build(dark: Set(("--brand", "VAR(--coral-600)"))).Css));
    }

    [Fact]
    public void A_dark_value_restates_the_librarys_light_value_where_it_has_one()
    {
        // --bg has a light value: a bare :root --bg would otherwise win in light too.
        var bg = Build(dark: Set(("--bg", "#101010")));
        Assert.Equal([new("--bg", "#101010")], bg.Root);
        Assert.Equal([new("--bg", "var(--slate-50)")], bg.Light);
        Assert.Contains(":root[data-variant=\"light\"] {\n    --bg: var(--slate-50);", bg.Css, StringComparison.Ordinal);
        Assert.Equal("#f8fafc", bg.LightColours["--bg"].Hex);

        // --brand has none, so light follows the dark value and no light block is written.
        var brand = Build(dark: Set(("--brand", "#123456")));
        Assert.Empty(brand.Light);
        Assert.Equal("#123456", brand.LightColours["--brand"].Hex);
        Assert.DoesNotContain("data-variant", brand.Css, StringComparison.Ordinal);
    }

    [Fact]
    public void A_light_value_alone_is_written_for_light_alone()
    {
        var result = Build(light: Set(("--accent", "#004466")));

        Assert.Empty(result.Root);
        Assert.Equal([new("--accent", "#004466")], result.Light);
        Assert.Equal("#59c3ff", result.DarkColours["--accent"].Hex);
        Assert.Equal("#004466", result.LightColours["--accent"].Hex);

        // The library's own light value is no change.
        Assert.Empty(Build(light: Set(("--accent", "var(--orbit-700)"))).Light);
    }

    [Theory]
    [InlineData("#16a34a", null, null)]
    [InlineData("#2563eb", "#0ea5b7", null)]
    [InlineData("#7c3aed", "#f59e0b", "#18181b")]
    [InlineData(BrandCssBuilder.SednaRed, BrandCssBuilder.OrbitBlue, BrandCssBuilder.DeepSpace)]
    public void The_generated_css_sets_only_tokens_the_library_declares(string brand, string? accent, string? canvas)
    {
        var dark = Set(("--radius-surface", "12px"), ("--fg", "#ffffff"));
        var light = Set(("--fg", "#000000"));
        var generated = Names(Build(BrandCssBuilder.Ramps(brand, accent, canvas), dark, light).Css);
        var library = Names(Assets.Css);

        Assert.NotEmpty(generated);
        var unknown = generated.Except(library).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(unknown.Count == 0, "The generated brand.css sets tokens the library does not declare: " + string.Join(", ", unknown));
    }

    [Fact]
    public void A_different_brand_colour_gives_a_different_brand_css()
    {
        var green = BrandCssBuilder.Ramps("#16a34a");
        var result = Build(green);

        Assert.Null(green.Problem);
        Assert.Contains("--coral-600", Names(result.Css));
        Assert.Contains("brand #16a34a", result.Css, StringComparison.Ordinal);
        // The step --brand points at carries white text, as the built-in themes' does.
        Assert.True(Colour.Contrast("#ffffff", Token(green, "--coral-600")) >= 4.5);
        Assert.Equal(Token(green, "--coral-600"), result.DarkColours["--brand"].Hex);
        // The accent and the base are left alone until they are asked for.
        Assert.DoesNotContain("--orbit-", result.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("--slate-", result.Css, StringComparison.Ordinal);
        Assert.Contains("--orbit-400", Build(BrandCssBuilder.Ramps("#16a34a", "#0ea5b7")).Css, StringComparison.Ordinal);
    }

    [Fact]
    public void A_canvas_colour_generates_the_base_ramp()
    {
        var start = BrandCssBuilder.Ramps(BrandCssBuilder.SednaRed, canvas: "#18181b");
        var result = Build(start);

        Assert.Null(start.Problem);
        Assert.Contains("--slate-900", Names(result.Css));
        Assert.DoesNotContain("--coral-", result.Css, StringComparison.Ordinal);
        Assert.Equal(Token(start, "--slate-900"), result.DarkColours["--bg"].Hex);
        // The light variant comes from the same ramp.
        Assert.Equal(Token(start, "--slate-50"), result.LightColours["--bg"].Hex);
    }

    [Fact]
    public void Sedna_red_gives_the_librarys_own_ramp_and_changes_nothing()
    {
        var sedna = BrandCssBuilder.Ramps(BrandCssBuilder.SednaRed, BrandCssBuilder.OrbitBlue, BrandCssBuilder.DeepSpace);

        Assert.Equal("#d73f1a", Token(sedna, "--coral-600"));
        Assert.Equal("#ff6b4a", Token(sedna, "--coral-500"));
        Assert.Equal(0, Build(sedna).Changed);
    }

    [Fact]
    public void A_value_that_is_no_colour_is_refused_and_said_so()
    {
        var result = BrandCssBuilder.Ramps("coral");

        Assert.Equal(BrandCssBuilder.SednaRed, result.Brand);
        Assert.NotNull(result.Problem);
    }

    [Theory]
    [InlineData("red; } body { display: none")]
    [InlineData("</style><script>")]
    [InlineData("#fff !important")]
    [InlineData("  ")]
    public void A_value_that_would_escape_its_declaration_is_refused(string value) =>
        Assert.NotNull(BrandCssBuilder.Refuse(value));

    // ── What it measures ───────────────────────────────────────────────────

    [Fact]
    public void A_brand_that_lands_on_a_semantic_family_fails_the_hue_check()
    {
        // Crimson, the danger family's own hue: brand and danger would read as one.
        var result = Build(BrandCssBuilder.Ramps("#c72268"));

        Assert.Contains(result.HueChecks, c => !c.Passes && c.Family == "danger");
    }

    [Fact]
    public void Text_moved_onto_its_own_background_fails_in_that_variant_only()
    {
        var result = Build(dark: Set(("--fg", "var(--slate-900)")), light: Set(("--fg", "var(--slate-950)")));
        var body = result.Checks.Single(c => c.Title == "Body text on the page");

        Assert.False(body.PassesIn(body.Dark));
        Assert.Equal(1.0, body.Dark.Ratio!.Value, 2);
        Assert.True(body.PassesIn(body.Light));
        Assert.False(body.Passes);
        Assert.False(string.IsNullOrWhiteSpace(body.Fix));
    }

    [Fact]
    public void A_light_brand_fails_the_label_on_the_filled_button()
    {
        // --brand at coral 500, the mistake docs/BRANDING.md §3.1 warns against: 2.82:1.
        var result = Build(dark: Set(("--brand", "var(--coral-500)")));
        var label = result.Checks.Single(c => c.Title == "Label on a filled brand button");

        Assert.False(label.Passes);
        Assert.InRange(label.Dark.Ratio!.Value, 2.7, 2.9);
    }

    [Fact]
    public void A_tint_is_measured_over_the_page_it_sits_on()
    {
        // An opaque, mid-strength go tint takes its text below 4.5:1.
        var result = Build(dark: Set(("--go-bg", "var(--green-700)")));
        var tint = result.Checks.Single(c => c.Title == "Go text on its tint");

        Assert.Equal(Library.DarkColours["--green-700"].Hex, tint.Dark.Background!.Value.Hex);
        Assert.False(tint.PassesIn(tint.Dark));
    }

    // ── The page ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_page_renders_the_editor_the_checks_and_the_brand_css_it_applies()
    {
        var html = await app.Client.GetStringAsync("/theme-builder");

        // Applied to the page, and printed for copying: the same text in both places.
        Assert.Matches(new Regex(@"<style>/\* brand.css: the Sedna.UI theme builder[^<]*</style>"), html);
        Assert.Contains("id=\"theme-css\"", html, StringComparison.Ordinal);
        Assert.Contains("data-copy-target", html, StringComparison.Ordinal);
        Assert.Contains("type=\"color\"", html, StringComparison.Ordinal);

        // The brand group is open, with a dark and a light field for --brand.
        Assert.Contains("data-token=\"--brand\" data-variant=\"dark\"", html, StringComparison.Ordinal);
        Assert.Contains("data-token=\"--brand\" data-variant=\"light\"", html, StringComparison.Ordinal);
        // Every group is in the picker.
        foreach (var group in Library.Groups)
            Assert.Contains($"value=\"{group.Slug}\"", html, StringComparison.Ordinal);

        Assert.Contains("Body text on the page", html, StringComparison.Ordinal);
        Assert.Contains("class=\"cat-pair-sample\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("tr--danger", html, StringComparison.Ordinal);
    }

    private static string Token(QuickStart start, string name) =>
        start.Tokens.Single(t => t.Key == name).Value;
}
