using System.Collections.ObjectModel;
using System.Text.Json;

namespace Sedna.UI;

/// <summary>
/// What this version of the library declares, and the release each name first shipped in —
/// for an app's own tests.
/// </summary>
/// <remarks>
/// <para>
/// A mistyped class does nothing at all, and a class newer than the pinned package does
/// nothing either, with no error anywhere. A test in the app can hold its markup to this:
/// </para>
/// <code>
/// foreach (var name in ClassesMyPagesUse())
///     Assert.True(SednaContract.Classes.ContainsKey(name), $".{name} is not a Sedna.UI class.");
/// </code>
/// <para>
/// The value is the release that first shipped the name, such as <c>"0.24.0"</c>, or null for
/// a name this build carries that no release has yet. It is read from the class history the
/// package was built with, generated from the release tags by <c>build/class-history.sh</c>.
/// </para>
/// </remarks>
public static class SednaContract
{
    private const string Resource = "Sedna.UI.class-history.json";

    private static readonly Lazy<History> Loaded = new(Load);

    /// <summary>The newest release this build knows of — the package's own version for a released package.</summary>
    public static string LatestRelease => Loaded.Value.LatestRelease;

    /// <summary>Every CSS class the stylesheet declares, without the dot, with the release that first shipped it.</summary>
    public static IReadOnlyDictionary<string, string?> Classes => Loaded.Value.Classes;

    /// <summary>Every design token the stylesheet declares, with the release that first shipped it.</summary>
    public static IReadOnlyDictionary<string, string?> Tokens => Loaded.Value.Tokens;

    private sealed record History(
        string LatestRelease,
        IReadOnlyDictionary<string, string?> Classes,
        IReadOnlyDictionary<string, string?> Tokens);

    private static History Load()
    {
        using var stream = typeof(SednaContract).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException(
                "This Sedna.UI assembly was built without its class history. Run build/class-history.sh "
                + "before building the library; a released package always carries it.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        return new History(
            root.GetProperty("latestRelease").GetString() ?? string.Empty,
            Map(root, "classes"),
            Map(root, "tokens"));
    }

    private static ReadOnlyDictionary<string, string?> Map(JsonElement root, string section) =>
        new(root.GetProperty(section).EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null,
                StringComparer.Ordinal));
}
