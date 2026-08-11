using System.Collections.Concurrent;
using System.Reflection;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// The repository's own documentation, embedded and served verbatim.
/// </summary>
/// <remarks>
/// Never retyped into a string here. <c>McpToolTests</c> asserts the host-page
/// block the server returns is a substring of <c>docs/getting-started.md</c>, which
/// is the natural successor to the guard that exists because every consuming app
/// copies that block.
/// </remarks>
internal static class Docs
{
    private static readonly Assembly Assembly = typeof(Docs).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>The names available, without their path.</summary>
    public static IEnumerable<string> Names =>
        Assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Docs.", StringComparison.Ordinal)
                        && n.EndsWith(".md", StringComparison.Ordinal))
            .Select(n => n[(n.IndexOf(".Docs.", StringComparison.Ordinal) + ".Docs.".Length)..])
            .OrderBy(n => n, StringComparer.Ordinal);

    public static string Read(string fileName) => Cache.GetOrAdd(fileName, static name =>
    {
        var resource = Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".Docs." + name, StringComparison.Ordinal));

        if (resource is null)
            throw new InvalidOperationException(
                $"docs/{name} is not embedded. The Dockerfile has to COPY docs/, and the "
                + $"EmbeddedResource glob has to cover it. Embedded: {string.Join(", ", Names)}");

        using var stream = Assembly.GetManifestResourceStream(resource)!;
        return new StreamReader(stream).ReadToEnd();
    });
}
