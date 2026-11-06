namespace Sedna.UI.Tests;

/// <summary>
/// A theme's own roles per variant: written for that theme and variant only, never in high
/// contrast, and nothing but a token name and a colour reaches the style element.
/// </summary>
public class ThemeRolesTests
{
    private static readonly SednaTheme Pink = new("pink", SednaTheme.Sedna.Palette)
    {
        LightRoles = new Dictionary<string, string> { ["--card-bg"] = "#fff0f6", ["--sidebar-bg"] = "var(--coral-50)" },
        DarkRoles = new Dictionary<string, string> { ["--card-bg"] = "var(--navy-950)" },
    };

    [Fact]
    public void Each_variant_gets_its_own_block_outside_high_contrast()
    {
        var css = SednaUiBrand.ToCss(new SednaUiOptions { Themes = [SednaTheme.Sedna, Pink], Default = "sedna" });

        Assert.Contains(":root[data-theme=\"pink\"][data-variant=\"light\"]:not([data-contrast=\"more\"]){--card-bg:#fff0f6;--sidebar-bg:var(--coral-50);}", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"pink\"]:not([data-variant=\"light\"],[data-contrast=\"more\"]){--card-bg:var(--navy-950);}", css, StringComparison.Ordinal);
        // The built-in theme sets no roles, so it gets no role block.
        Assert.DoesNotContain("[data-theme=\"sedna\"],:not([data-theme]))", css, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_theme_s_roles_apply_before_a_theme_is_set()
    {
        var css = SednaUiBrand.ToCss(new SednaUiOptions { Themes = [Pink], Default = "pink" });
        Assert.Contains(":root:is([data-theme=\"pink\"],:not([data-theme]))[data-variant=\"light\"]", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("card-bg", "#fff")]
    [InlineData("--card-bg}", "#fff")]
    [InlineData("--card-bg", "red")]
    [InlineData("--card-bg", "#fff;}</style><script>")]
    [InlineData("--card-bg", "var(--x));")]
    public void A_name_or_value_of_any_other_shape_is_refused(string name, string value) =>
        Assert.Throws<ArgumentException>(() => new SednaTheme("t", SednaTheme.Sedna.Palette)
        {
            DarkRoles = new Dictionary<string, string> { [name] = value },
        });
}
