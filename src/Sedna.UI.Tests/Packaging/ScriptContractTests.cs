using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The script's global name, its storage prefix, and the two things it must not do.
/// </summary>
public class ScriptContractTests
{
    [Fact]
    public void The_javascript_global_is_sednaUi()
    {
        var js = File.ReadAllText(Assets.JsPath);
        Assert.Contains("window.sednaUi", js, StringComparison.Ordinal);
        Assert.Contains("configure", js, StringComparison.Ordinal);
    }

    [Fact]
    public void The_boot_script_and_the_main_script_share_a_default_storage_prefix()
    {
        // They read and write the same localStorage keys. If the defaults ever
        // disagree, an app that does not configure a prefix loses its theme on
        // every reload — the boot script would stamp one set of values and the
        // main script another.
        // Comments are stripped first — both files document a `storagePrefix:
        // 'myapp.'` usage example, which would otherwise match before the default.
        var boot = StripJsComments(File.ReadAllText(Assets.BootJsPath));
        var main = StripJsComments(File.ReadAllText(Assets.JsPath));

        var bootPrefix = Regex.Match(boot, @"dataset\.prefix\)\s*\|\|\s*'(?<p>[^']+)'").Groups["p"].Value;
        var mainPrefix = Regex.Match(main, @"storagePrefix:\s*'(?<p>[^']+)'").Groups["p"].Value;

        Assert.False(string.IsNullOrEmpty(bootPrefix), "Could not find the boot script's default prefix.");
        Assert.Equal(bootPrefix, mainPrefix);
    }

    [Fact]
    public void The_boot_script_and_the_main_script_both_read_the_variant_key()
    {
        // data-variant is a second stored setting the two scripts must agree about,
        // alongside the prefix: boot.js resolves it before first paint, and
        // settings.js keeps it current afterwards (and both fall back through the
        // same "system" -> prefers-color-scheme path). A typo in either file's key
        // name would have the two silently stop meeting in localStorage.
        var boot = StripJsComments(File.ReadAllText(Assets.BootJsPath));
        var main = StripJsComments(File.ReadAllText(Assets.JsPath));

        Assert.Contains("get('variant')", boot, StringComparison.Ordinal);
        Assert.Contains("key('variant')", main, StringComparison.Ordinal);
    }

    /// <summary>
    /// Removes JS comments. The line-comment rule skips a <c>//</c> preceded by a
    /// colon so URLs inside string literals survive.
    /// </summary>
    private static string StripJsComments(string js)
    {
        js = Regex.Replace(js, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(js, @"(?<!:)//[^\n]*", string.Empty);
    }

    [Fact]
    public void The_scripts_name_nothing_real()
    {
        // Shapes, not names. The shipped script is served publicly by every app that
        // installs the package and its comments are part of what ships, so an example
        // ticket number in one of them is a customer's record number in somebody else's
        // browser. A literal deny-list would be a published list of the very names it
        // forbids, so what is matched is the FORM of a real record, host or company —
        // see the `No real names` section of the repo's CLAUDE.md for the rest.
        //
        // Read with comments intact, deliberately: a snippet inside one is as published
        // as a line of code.
        foreach (var path in new[] { Assets.JsPath, Assets.BootJsPath })
        {
            var js = File.ReadAllText(path);
            var found = RealWorldShapes.FoundIn(js);

            Assert.True(found.Count == 0,
                $"{Path.GetFileName(path)} names something real: {string.Join(", ", found)}");
        }
    }

    [Fact]
    public void The_stylesheet_names_nothing_real()
    {
        // The same shapes for the sheet, which ships and is served publicly too, and
        // whose comments carry markup snippets — a first name used as demo content sat
        // in one of them and reached every installing app.
        var found = RealWorldShapes.FoundIn(File.ReadAllText(Assets.CssPath));

        Assert.True(found.Count == 0,
            $"The stylesheet names something real: {string.Join(", ", found)}");
    }

    [Fact]
    public void The_tip_engine_leaves_the_sidebar_to_its_own_css_flyout()
    {
        // Both firing produces a double tooltip on the collapsed rail. The CSS
        // side is `.sidebar.collapsed [data-tip]:hover::after`; this is the other
        // half of that contract.
        var js = File.ReadAllText(Assets.JsPath);
        Assert.Contains("closest('.sidebar')", js, StringComparison.Ordinal);
    }
}
