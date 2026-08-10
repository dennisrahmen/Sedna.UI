using System.Collections.Concurrent;
using System.Reflection;

namespace Sedna.UI.Catalogue.Components.Docs;

/// <summary>
/// Reads an example's own source text out of the assembly.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a demo and the code block beside it the same bytes. The
/// <c>.razor</c> file under <c>Examples/</c> is compiled into a component
/// <i>and</i> embedded as a resource, so the page renders the component and prints
/// the file — there is no second copy of the markup to drift.
/// </para>
/// <para>
/// The manifest name is <c>$(RootNamespace)</c> plus the relative path with
/// separators replaced by dots, which is exactly how the Razor SDK derives the
/// component's namespace. <c>typeof(Examples.Badge.Semantic).FullName + ".razor"</c>
/// is therefore the resource name — but the two are computed by different code
/// paths from the same inputs, so a folder name that is not a valid C# identifier
/// desynchronises them. That failure is a blank code block, not a build error,
/// which is why <c>ExampleRenderTests</c> asserts no rendered block is empty.
/// </para>
/// </remarks>
internal static class ExampleSource
{
    private static readonly Assembly Assembly = typeof(ExampleSource).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>The source of a live example, keyed by its component type.</summary>
    public static string For(Type example)
    {
        ArgumentNullException.ThrowIfNull(example);
        return Read(example.FullName + ".razor");
    }

    /// <summary>
    /// The source of a code-only snippet, by its path under <c>Examples/</c> —
    /// for example <c>"Index/HostPage.html"</c>.
    /// </summary>
    /// <remarks>
    /// Matched by suffix rather than by a hard-coded namespace prefix, so the one
    /// place <c>RootNamespace</c> is written stays the csproj.
    /// </remarks>
    public static string For(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        var suffix = ".Examples." + relative.Replace('/', '.');
        var name = Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));

        return name is null ? throw Missing(suffix) : Read(name);
    }

    /// <summary>Every embedded example source, keyed by manifest name.</summary>
    /// <remarks>Used by the MCP index and by the guard tests.</remarks>
    public static IEnumerable<string> Names =>
        Assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Examples.", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

    private static string Read(string resource) => Cache.GetOrAdd(resource, static name =>
    {
        using var stream = Assembly.GetManifestResourceStream(name) ?? throw Missing(name);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        // Normalised here as well as in .gitattributes: the printed snippet must
        // not differ by the machine that built it.
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    });

    private static InvalidOperationException Missing(string name) =>
        new($"""
             No embedded example source matching "{name}".

             Either the EmbeddedResource glob in Sedna.UI.Catalogue.csproj does not cover the
             file, or a folder under Examples/ is not a valid C# identifier — MSBuild and the Razor
             SDK mangle those differently, so the manifest name and the component namespace stop
             agreeing.

             Embedded: {string.Join(", ", Assembly.GetManifestResourceNames())}
             """);
}
