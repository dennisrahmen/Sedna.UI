using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// Where the catalogue application's own source lives. The shipped library assets
/// are located by <see cref="Assets"/>, which is linked in from the library's
/// suite rather than copied.
/// </summary>
internal static class CatalogueAssets
{
    public static string AppDir { get; } =
        Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Catalogue");

    public static string ExamplesDir => Path.Combine(AppDir, "Examples");
    public static string PagesDir => Path.Combine(AppDir, "Components", "Pages");
    public static string LayoutDir => Path.Combine(AppDir, "Components", "Layout");
    public static string NavigationDir => Path.Combine(AppDir, "Navigation");
    public static string CatalogueCssPath => Path.Combine(AppDir, "wwwroot", "catalogue.css");

    /// <summary>
    /// The one remote host the catalogue may load from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>package</b> loads nothing remote and that is not negotiable: everything it
    /// needs ships inside it, so no host's outage can affect a customer's site. The
    /// catalogue is a different thing — a web server we operate, whose job is to teach —
    /// and a page about images cannot teach how an <c>&lt;img&gt;</c> is handled with no
    /// image in it. So this one host is allowed, here and in an example, and nowhere near
    /// the library.
    /// </para>
    /// <para>
    /// One host, named in one place, so the three tests that care cannot drift: the two
    /// example guards below and the console-error check in <c>PageLoadTests</c>, which has
    /// to tolerate a runner with no egress rather than fail on one.
    /// </para>
    /// </remarks>
    public const string PhotoHost = "images.unsplash.com";

    /// <summary>
    /// Every file the catalogue's content is written in: the example sources, the
    /// pages, the components and the registry.
    /// </summary>
    /// <remarks>
    /// <c>Examples/</c> is the whole tree rather than <c>*.razor</c> — the code-only
    /// snippets document classes too. <c>wwwroot/catalogue.css</c> is deliberately
    /// excluded, matching what the static catalogue's coverage test did: a comment
    /// in the docs' own chrome mentioning a class is not documentation of it.
    /// </remarks>
    public static IEnumerable<string> ContentFiles()
    {
        foreach (var (dir, pattern) in new[]
                 {
                     (ExamplesDir, "*"),
                     (Path.Combine(AppDir, "Components"), "*.razor"),
                     (NavigationDir, "*.cs"),
                 })
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                yield return file;
        }
    }
}
