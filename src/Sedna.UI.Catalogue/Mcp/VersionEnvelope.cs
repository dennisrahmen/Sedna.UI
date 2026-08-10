using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>The <c>meta</c> block on every MCP response.</summary>
internal sealed record Meta(
    string Source,
    string Commit,
    string BuiltUtc,
    string LatestRelease,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InstalledVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning);

/// <summary>
/// Says which release first shipped each class and token, and warns when an agent
/// asks about something its installed version does not have.
/// </summary>
/// <remarks>
/// <para>
/// The failure this exists for is precise: an agent copies markup for a class its
/// app's pinned version does not contain, and the page renders unstyled with no
/// error anywhere.
/// </para>
/// <para>
/// Results are still returned when they are too new. Filtering them out silently
/// would hide that an upgrade is the fix, which is usually the right answer.
/// </para>
/// </remarks>
internal sealed class VersionEnvelope
{
    private readonly Dictionary<string, string?> _classes;
    private readonly Dictionary<string, string?> _tokens;

    public VersionEnvelope()
    {
        var assembly = typeof(VersionEnvelope).Assembly;

        using var stream = assembly.GetManifestResourceStream(
                               assembly.GetManifestResourceNames()
                                   .First(n => n.EndsWith("class-history.json", StringComparison.Ordinal)))
                           ?? throw new InvalidOperationException(
                               "class-history.json is not embedded. Run build/class-history.sh.");

        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        LatestRelease = root.GetProperty("latestRelease").GetString() ?? "0.0.0";
        _classes = Map(root.GetProperty("classes"));
        _tokens = Map(root.GetProperty("tokens"));

        // Baked at image build time via -p:SourceRevisionId. Not a runtime `git`
        // call: .git is excluded from the Docker context on purpose.
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        Commit = plus >= 0 ? informational[(plus + 1)..] : "unknown";

        BuiltUtc = File.GetLastWriteTimeUtc(assembly.Location).ToString("O");
    }

    public string LatestRelease { get; }

    public string Commit { get; }

    public string BuiltUtc { get; }

    /// <summary>The release a class first shipped in, or null if it is unreleased.</summary>
    public string? SinceClass(string name) => _classes.GetValueOrDefault(name.TrimStart('.'));

    /// <summary>The release a token first shipped in, or null if it is unreleased.</summary>
    public string? SinceToken(string name) => _tokens.GetValueOrDefault(name);

    /// <summary>The newest release among a set of classes — an example's own floor.</summary>
    public string? SinceAll(IEnumerable<string> classes)
    {
        string? newest = null;
        foreach (var name in classes)
        {
            if (!_classes.TryGetValue(name.TrimStart('.'), out var since)) continue;
            // A single unreleased class makes the whole example unreleased.
            if (since is null) return null;
            if (newest is null || Compare(since, newest) > 0) newest = since;
        }

        return newest ?? LatestRelease;
    }

    /// <summary>
    /// Builds the envelope, naming anything the caller's installed version does not
    /// have.
    /// </summary>
    public Meta For(string? installedVersion, IEnumerable<(string Name, string? Since)>? items = null)
    {
        string? warning = null;

        if (installedVersion is not null && items is not null)
        {
            var missing = items
                .Where(i => i.Since is null || Compare(i.Since, installedVersion) > 0)
                .Select(i => i.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            if (missing.Count > 0)
            {
                warning =
                    $"{missing.Count} of these are not in {installedVersion}: "
                    + string.Join(", ", missing.Take(12))
                    + (missing.Count > 12 ? ", …" : string.Empty)
                    + ". Upgrade Sedna.UI, or use something else.";
            }
        }

        return new Meta("main", Commit, BuiltUtc, LatestRelease, installedVersion, warning);
    }

    /// <summary>Compares two SemVer-ish versions numerically, not as text.</summary>
    /// <remarks><c>0.10.0</c> is newer than <c>0.9.0</c>; a string compare says otherwise.</remarks>
    internal static int Compare(string left, string right)
    {
        var a = Parse(left);
        var b = Parse(right);

        for (var i = 0; i < 3; i++)
        {
            var order = a[i].CompareTo(b[i]);
            if (order != 0) return order;
        }

        return 0;
    }

    private static int[] Parse(string version)
    {
        var core = version.Split('-')[0].Split('+')[0].Split('.');
        var parts = new int[3];
        for (var i = 0; i < 3; i++)
            parts[i] = i < core.Length && int.TryParse(core[i], out var n) ? n : 0;

        return parts;
    }

    private static Dictionary<string, string?> Map(JsonElement element) =>
        element.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString(),
            StringComparer.Ordinal);
}
