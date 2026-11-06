using System.Reflection;
using System.Text.Json;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// The class renames each release made, read from <c>docs/renames/&lt;version&gt;.json</c> as
/// embedded in this app: an old class name and the class or classes that look the same now.
/// </summary>
/// <remarks>
/// An agent upgrading an app asks about the names the app already uses. A name a release
/// renamed is not in the stylesheet any more, and "no such class" sends it to search for a
/// replacement it may guess wrong — <c>.btn-go</c> becomes two classes, not one. So the
/// server answers an old name with what replaced it.
/// </remarks>
internal static class Renames
{
    public sealed record Rename(string Old, IReadOnlyList<string> New, string Release, string From);

    public sealed record Map(string Release, string From, string About, IReadOnlyList<Rename> Renames);

    private static readonly Lazy<IReadOnlyList<Map>> Loaded = new(Load);

    public static IReadOnlyList<Map> All => Loaded.Value;

    /// <summary>The rename of a class, with or without its dot, or null.</summary>
    public static Rename? Of(string name)
    {
        var bare = name.TrimStart('.');
        return All.SelectMany(m => m.Renames).LastOrDefault(r => string.Equals(r.Old, bare, StringComparison.Ordinal));
    }

    private static IReadOnlyList<Map> Load()
    {
        var assembly = typeof(Renames).Assembly;
        var maps = new List<Map>();
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.Contains(".Renames.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var release = root.GetProperty("release").GetString()!;
            var from = root.GetProperty("from").GetString()!;
            var renames = root.GetProperty("renames").EnumerateArray()
                .Select(r => new Rename(
                    r.GetProperty("old").GetString()!,
                    r.GetProperty("new").EnumerateArray().Select(n => n.GetString()!).ToList(),
                    release, from))
                .ToList();
            maps.Add(new Map(release, from, root.GetProperty("about").GetString() ?? "", renames));
        }
        return maps.OrderBy(m => Version.Parse(m.Release)).ToList();
    }
}
