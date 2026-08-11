using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The vendored icon font: its licence, the one font file it may reference, and the version the notice claims.
/// </summary>
public class IconTests
{
    [Fact]
    public void The_bundled_icon_font_ships_its_licence_and_copyright()
    {
        // Section 5 of the Remix Icon License: when redistributing the complete
        // library, retain the copyright notices and include a copy of the licence.
        // Both are conditions of being allowed to ship the font at all.
        var dir = Path.Combine(Assets.ProjectDir, "wwwroot", "lib", "remixicon");

        var licence = File.ReadAllText(Path.Combine(dir, "LICENSE"));
        Assert.Contains("Remix Icon License", licence, StringComparison.Ordinal);
        Assert.Contains("Remix Design", licence, StringComparison.Ordinal);

        // The upstream header must survive vendoring.
        var css = File.ReadAllText(Path.Combine(dir, "remixicon.css"));
        Assert.Contains("Copyright RemixIcon.com", css, StringComparison.Ordinal);
        Assert.Contains("remixicon.com", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_bundled_icon_font_references_only_the_woff2_that_ships()
    {
        // vendor-remixicon.sh trims the src list to woff2. If a full upstream CSS
        // is dropped in by hand, the browser requests .eot/.woff/.ttf/.svg files
        // that were never packed — four 404s per page load.
        var css = File.ReadAllText(Path.Combine(
            Assets.ProjectDir, "wwwroot", "lib", "remixicon", "remixicon.css"));

        var urls = Regex.Matches(css, @"url\(([^)]*)\)")
            .Select(m => m.Groups[1].Value.Trim('"', '\''))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["remixicon.woff2"], urls);
    }

    [Fact]
    public void The_third_party_notice_states_the_version_that_is_actually_vendored()
    {
        // Guards the drift that attribution documents always suffer: the font gets
        // updated and the notice keeps claiming the old version.
        var notices = File.ReadAllText(Path.Combine(Assets.RepoRoot, "THIRD-PARTY-NOTICES.md"));
        Assert.Contains("Remix Icon", notices, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(
            Assets.ProjectDir, "wwwroot", "lib", "remixicon", "remixicon.css"));

        var vendored = Regex.Match(css, @"Remix Icon v(?<v>\d+\.\d+\.\d+)").Groups["v"].Value;
        Assert.False(string.IsNullOrEmpty(vendored), "Could not read the version from the vendored CSS header.");

        Assert.True(notices.Contains(vendored, StringComparison.Ordinal),
            $"THIRD-PARTY-NOTICES.md does not mention the vendored version {vendored}. " +
            "Update it, and the version in the docs, after running build/vendor-remixicon.sh.");
    }
}
