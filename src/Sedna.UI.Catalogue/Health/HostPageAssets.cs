using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace Sedna.UI.Catalogue.Health;

/// <summary>
/// The local files the host page loads, read out of the host page itself.
/// </summary>
/// <remarks>
/// Nothing here is hand-listed, for the same reason the list exists at all: the
/// site shipped once with no <c>_framework/blazor.web.js</c>, every page returned
/// 200 and nothing on it was interactive. A written-out list would have to be
/// extended by whoever adds the next asset, which is the person least likely to
/// know it is there.
/// </remarks>
internal static class HostPageAssets
{
    private static readonly Regex Reference = new(
        @"<(?:script[^>]*\ssrc|link[^>]*\shref)=""(?<path>[^""]+)""", RegexOptions.Compiled);

    /// <summary>The paths, relative to the web root, in host-page order.</summary>
    public static IReadOnlyList<string> All { get; } = Read();

    /// <summary>
    /// Those the app would 404, resolved through the provider that serves them —
    /// so this reports what a browser would get, not what is on disk.
    /// </summary>
    public static IReadOnlyList<string> Missing(IFileProvider webRoot)
    {
        ArgumentNullException.ThrowIfNull(webRoot);

        return [.. All.Where(path => !webRoot.GetFileInfo(path).Exists)];
    }

    private static string[] Read()
    {
        const string Resource = "Sedna.UI.Catalogue.Components.App.razor";

        using var stream = typeof(HostPageAssets).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException(
                $"\"{Resource}\" is not embedded, so the health endpoint has nothing to check. "
                + "Add Components/App.razor to the EmbeddedResource items in the csproj.");

        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        return
        [
            .. Reference.Matches(source)
                .Select(match => match.Groups["path"].Value)
                .Where(IsLocal)
                .Distinct(StringComparer.Ordinal)
        ];
    }

    // A remote URL is out of scope and banned anyway, an in-page anchor is not a
    // file, and an @ expression is not a literal path. The host page states plain
    // paths deliberately, so none of these should ever match.
    private static bool IsLocal(string path) =>
        !path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith('#')
        && !path.Contains('@', StringComparison.Ordinal);
}
