using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every class the stylesheet defines is shown somewhere in the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// A class nobody can find is a class nobody uses, and then someone writes it again
/// in an app. The first run of this test found 97 undocumented classes.
/// </para>
/// <para>
/// <b>There is no allowlist here and there should never be one.</b> The static
/// catalogue carried three Blazor-owned reconnect classes on an exception list; the
/// Shell &amp; nav page shows all three now, so it is gone.
/// </para>
/// <para>
/// A mention in a page's prose counts as documentation. That is how
/// <c>.menu-scrim</c> is covered — it cannot be demonstrated live without a
/// full-viewport element swallowing every click on the page — and how the three
/// Blazor-owned <c>components-reconnect-*</c> classes are, which an app never
/// writes but may need to recognise.
/// </para>
/// <para>
/// The <c>ql-</c> classes are skipped: they are the markup Quill writes inside the
/// editor's <c>.editor-body</c>, which <c>44-rich-text.css</c> dresses and an app never
/// writes. The <c>sedna-mochi-</c> classes are skipped for the same reason: they are the
/// parts of Mochi's drawing, inside the sprite <c>SednaMochi</c> writes, and an app
/// writes <c>.mochi</c> and its attributes, never a part. <c>MochiSpriteTests</c> holds
/// them to the stylesheet in both directions instead.
/// </para>
/// <para>
/// The second test is stricter: a class an app writes is in an <i>example</i>, the
/// markup a reader copies, and not only named in a page's prose. A class nobody writes
/// is exempt by where it comes from, never by a list — the library's own script writes
/// it (a whole word of a string literal in a shipped script), or Blazor's reconnect
/// UI owns it. The state-art drawing classes are not exempt: an app draws its own
/// scene with them.
/// </para>
/// </remarks>
public class CoverageTests
{
    private static ISet<string> Declared() =>
        Assets.ClassSelectors(Assets.StripComments(Assets.Css));

    [Fact]
    public void Every_class_in_the_stylesheet_appears_somewhere_in_the_catalogue_source()
    {
        // The examples, the pages, the app's own layout, and the registry — the same
        // haystack shape the static catalogue used, over the files that replaced it.
        var haystack = string.Concat(
            CatalogueAssets.ContentFiles()
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        // A `ql-` class is markup Quill writes inside the editor, and a `sedna-mochi-` class
        // is a part of Mochi's sprite; the stylesheet dresses both and the app writes neither.
        var missing = Declared()
            .Where(name => !name.StartsWith("ql-", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("sedna-mochi-", StringComparison.Ordinal))
            .Where(name => !haystack.Contains(name, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} classes are in the stylesheet but nowhere in the catalogue. "
            + "A class with no page is a class nobody can find: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_class_an_app_writes_is_in_an_example()
    {
        var examples = string.Concat(
            Directory.EnumerateFiles(CatalogueAssets.ExamplesDir, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var inExamples = new HashSet<string>(
            Regex.Matches(examples, @"[A-Za-z0-9_-]+").Select(m => m.Value), StringComparer.Ordinal);

        var scriptWritten = ScriptWrittenWords();

        var missing = Declared()
            .Where(name => !name.StartsWith("ql-", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("sedna-mochi-", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("components-reconnect-", StringComparison.Ordinal))
            .Where(name => !scriptWritten.Contains(name))
            .Where(name => !inExamples.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} classes an app writes are in no example under Examples/ — only "
            + "named in prose, which a reader cannot copy. Show each in the markup of an example: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// Every whitespace-separated word of every string literal in the shipped scripts —
    /// the class names the library writes itself, as <c>classList.add('x')</c> or
    /// <c>className = 'x y'</c> do. A selector such as <c>'.x'</c> is a different word, so
    /// a class the script only reads is not counted.
    /// </summary>
    private static ISet<string> ScriptWrittenWords()
    {
        var scripts = Directory.EnumerateFiles(Path.Combine(Assets.WwwrootDir, "js"), "*.js")
            .Append(Path.Combine(Assets.WwwrootDir, "Sedna.UI.lib.module.js"))
            .Select(File.ReadAllText);
        var literal = new Regex(@"'(?:[^'\\\n]|\\.)*'|""(?:[^""\\\n]|\\.)*""|`[^`]*`");
        return new HashSet<string>(
            scripts.SelectMany(js => literal.Matches(js))
                .SelectMany(m => m.Value[1..^1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
            StringComparer.Ordinal);
    }
}
