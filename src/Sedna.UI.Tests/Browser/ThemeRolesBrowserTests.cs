using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>A theme's light roles reach the page in light, and not in high contrast or dark.</summary>
public class ThemeRolesBrowserTests : ScriptTestBase
{
    [Theory]
    [InlineData("light", "", "rgb(255, 240, 246)")]
    [InlineData("light", "more", null)]
    [InlineData("dark", "", null)]
    public async Task The_role_applies_in_its_variant_only(string variant, string contrast, string? expected)
    {
        if (NoBrowser) return;
        var theme = new SednaTheme("pink", SednaTheme.Sedna.Palette)
        {
            LightRoles = new Dictionary<string, string> { ["--card-bg"] = "#fff0f6" },
        };
        var css = SednaUiBrand.ToCss(new SednaUiOptions { Themes = [SednaTheme.Sedna, theme], Default = "sedna" });
        var (page, errors) = await OpenStyled("""<div class="card" id="c">Card</div>""", extraHead: $"<style>{css}</style>");

        var bg = await page.EvaluateAsync<string>("""
            ([v, c]) => {
              const r = document.documentElement;
              r.setAttribute('data-theme', 'pink'); r.setAttribute('data-variant', v);
              if (c) r.setAttribute('data-contrast', c); else r.removeAttribute('data-contrast');
              return getComputedStyle(document.getElementById('c')).backgroundColor;
            }
            """, new[] { variant, contrast });

        if (expected is null) Assert.NotEqual("rgb(255, 240, 246)", bg);
        else Assert.Equal(expected, bg);
        Assert.Empty(errors);
    }
}
