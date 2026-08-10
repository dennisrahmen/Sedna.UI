using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What decides whether an app can override a rule: no !important, and a documented z-order.
/// </summary>
public class OverrideTests
{
    [Fact]
    public void The_library_uses_no_important_declarations()
    {
        // !important defeats the override model twice. Unlayered, it beats an app's
        // ordinary override. Layered, it is worse: layer order inverts for important
        // declarations, so a layered !important outranks an app's own !important and
        // the app has no way left to win. Raise specificity instead.
        var css = Assets.StripComments(Assets.Css);

        var offenders = css.Split('\n')
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Where(x => x.Line.Contains("!important", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"line {x.Number}: {x.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "An app must always be able to override a library rule, so the library declares nothing " +
            "!important. Give the rule enough specificity to win on its own instead:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void Every_z_index_comes_from_the_documented_scale()
    {
        // The documented overlay scale in docs/architecture.md and CLAUDE.md. A new
        // overlay picks one of these; it does not invent a value, because the only
        // way to reason about six overlay families is for the list to be closed.
        // 0 and 1 are allowed for local stacking inside a component (a sticky table
        // header above its own rows), which is not part of the overlay scale.
        int[] documented = [0, 1, 60, 200, 400, 480, 490, 500, 510, 550, 600, 1000];

        // The catalogue app's own stylesheet is included: it draws on the same scale,
        // and its drawer once sat on the spotlight rung with its scrim on the
        // modal-backdrop rung, which would have interleaved a drawer with a real
        // modal — precisely the mistake a shared scale exists to prevent.
        //
        // It is no longer a stylesheet that ships. It is still held to the scale,
        // because the site is an application built with this library and a bad rung
        // there is a bad rung anywhere.
        var catalogueCss = Path.Combine(
            Assets.RepoRoot, "src", "Sedna.UI.Catalogue", "wwwroot", "catalogue.css");

        var sheets = new[]
        {
            (Name: "Sedna.UI.css", Css: Assets.StripComments(Assets.Css)),
            (Name: "Sedna.UI.Catalogue/catalogue.css",
             Css: Assets.StripComments(File.ReadAllText(catalogueCss))),
        };

        var offenders = sheets
            .SelectMany(s => Regex.Matches(s.Css, @"z-index\s*:\s*(-?\d+)", RegexOptions.Compiled)
                .Select(m => (s.Name, Value: int.Parse(m.Groups[1].Value))))
            .Where(x => !documented.Contains(x.Value))
            .Distinct()
            .OrderBy(x => x.Value)
            .Select(x => $"{x.Name}: {x.Value}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "These z-index values are not on the documented scale. Either use a documented layer or " +
            "add the new layer to the scale in docs/architecture.md and CLAUDE.md first, so the " +
            $"ordering stays reviewable: {string.Join(", ", offenders)}");
    }
}
