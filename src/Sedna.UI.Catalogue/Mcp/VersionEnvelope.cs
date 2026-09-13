using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol;

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
/// One <c>class-history.json</c>, parsed: the release each class, token, public C#
/// member and example first shipped in, and null for one in no release.
/// </summary>
/// <remarks>
/// Two of these exist at runtime — the copy embedded at build, and the copy the
/// latest GitHub release carries (<see cref="ReleasedHistory"/>) — so the parsing
/// lives here rather than in the envelope that merges them.
/// </remarks>
internal sealed record HistoryMaps(
    string LatestRelease,
    Dictionary<string, string?> Classes,
    Dictionary<string, string?> Tokens,
    Dictionary<string, string?> Csharp,
    Dictionary<string, string?> Examples,
    Dictionary<string, string?> ByMemberName)
{
    public static HistoryMaps Parse(JsonElement root)
    {
        var csharp = Map(root.GetProperty("csharp"));

        // An example mentions `RegisterCommandsAsync`, not `ISednaUi.RegisterCommandsAsync`,
        // so the bare name has to resolve too. Where two types declare the same member —
        // `Href` is on both PaletteCommand and SearchItem — the OLDEST wins: the question
        // this answers is "can my version write this", and either type having had it since
        // 0.2.0 makes the answer yes.
        var byMemberName = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, since) in csharp)
        {
            var dot = key.IndexOf('.', StringComparison.Ordinal);
            var bare = dot < 0 ? key : key[(dot + 1)..];
            if (!byMemberName.TryGetValue(bare, out var held))
            {
                byMemberName[bare] = since;
                continue;
            }

            if (held is null || (since is not null && VersionEnvelope.Compare(since, held) < 0))
                byMemberName[bare] = since;
        }

        return new HistoryMaps(
            root.GetProperty("latestRelease").GetString() ?? "0.0.0",
            Map(root.GetProperty("classes")),
            Map(root.GetProperty("tokens")),
            csharp,
            Map(root.GetProperty("examples")),
            byMemberName);
    }

    private static Dictionary<string, string?> Map(JsonElement element) =>
        element.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString(),
            StringComparer.Ordinal);
}

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
/// <para>
/// Usually, not always: something with no <c>since</c> at all is in no release, so
/// there is nothing to upgrade to and "upgrade Sedna.UI" is advice the caller
/// cannot take. The warning says the two separately for that reason.
/// </para>
/// <para>
/// Two copies of the history answer here. The embedded one is what the build had; a
/// <c>null</c> in it means "in no release <em>as far as this checkout knew</em>",
/// which between a tag and the first merge after it understates every entry the
/// tag shipped. The latest release's own copy (<see cref="ReleasedHistory"/>) was
/// generated at the tag and is asked only where the embedded copy says null, so it
/// can only ever move an entry from "unreleased" to the release that shipped it.
/// </para>
/// </remarks>
internal sealed class VersionEnvelope
{
    private readonly HistoryMaps _embedded;
    private readonly ReleasedHistory _released;

    public VersionEnvelope(ReleasedHistory released) : this(Embedded(), released, ResolveCommit(
        typeof(VersionEnvelope).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion,
        Environment.GetEnvironmentVariable))
    {
    }

    internal VersionEnvelope(HistoryMaps embedded, ReleasedHistory released, string commit)
    {
        _embedded = embedded;
        _released = released;
        Commit = commit;
        BuiltUtc = File.GetLastWriteTimeUtc(typeof(VersionEnvelope).Assembly.Location).ToString("O");
    }

    private static HistoryMaps Embedded()
    {
        var assembly = typeof(VersionEnvelope).Assembly;

        using var stream = assembly.GetManifestResourceStream(
                               assembly.GetManifestResourceNames()
                                   .First(n => n.EndsWith("class-history.json", StringComparison.Ordinal)))
                           ?? throw new InvalidOperationException(
                               "class-history.json is not embedded. Run build/class-history.sh.");

        using var document = JsonDocument.Parse(stream);
        return HistoryMaps.Parse(document.RootElement);
    }

    /// <summary>
    /// The newest release either copy knows. The embedded copy is behind between a tag
    /// and the first merge after it; the release's own copy is not.
    /// </summary>
    public string LatestRelease
    {
        get
        {
            var released = _released.Current?.LatestRelease;
            return released is not null && Compare(released, _embedded.LatestRelease) > 0
                ? released
                : _embedded.LatestRelease;
        }
    }

    public string Commit { get; }

    public string BuiltUtc { get; }

    /// <summary>The commit this site was built from.</summary>
    /// <remarks>
    /// Baked at image build time via <c>-p:SourceRevisionId</c>, never a runtime
    /// <c>git</c> call — <c>.git</c> is excluded from the Docker context on purpose.
    /// The environment is the fallback for the host that actually runs this. Every
    /// candidate is treated as absent when it is empty: a Railway variable set to
    /// <c>${{RAILWAY_GIT_COMMIT_SHA}}</c> renders to an empty string when the platform
    /// has no git variables to resolve, and an empty commit is worse than "unknown"
    /// because it looks like a field that was never populated rather than one that
    /// could not be.
    /// </remarks>
    internal static string ResolveCommit(string? informationalVersion, Func<string, string?> environment)
    {
        var informational = informationalVersion ?? string.Empty;
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0) return Reported(informational[(plus + 1)..]);

        return Reported(environment("SOURCE_COMMIT")) is var fromBuild and not "unknown"
            ? fromBuild
            : Reported(environment("RAILWAY_GIT_COMMIT_SHA"));
    }

    private static string Reported(string? commit) =>
        string.IsNullOrWhiteSpace(commit) ? "unknown" : commit.Trim();

    /// <summary>The release a class first shipped in, or null if it is unreleased.</summary>
    public string? SinceClass(string name) => Since(m => m.Classes, name.TrimStart('.'));

    /// <summary>The release a token first shipped in, or null if it is unreleased.</summary>
    public string? SinceToken(string name) => Since(m => m.Tokens, name);

    /// <summary>The release a public C# type or member first shipped in.</summary>
    /// <remarks>
    /// Accepts either <c>ISednaUi.ToastAsync</c> or the bare <c>ToastAsync</c>. Returns
    /// null for an unreleased member and, deliberately, also for a name the library does
    /// not declare — the caller only ever asks about names the index matched.
    /// </remarks>
    public string? SinceMember(string name) =>
        _embedded.Csharp.ContainsKey(name)
            ? Since(m => m.Csharp, name)
            : Since(m => m.ByMemberName, name);

    /// <summary>Whether a name is one the C# history knows.</summary>
    public bool KnowsMember(string name) =>
        _embedded.Csharp.ContainsKey(name) || _embedded.ByMemberName.ContainsKey(name);

    /// <summary>
    /// The floor for one example: the newest release among everything it uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An example's floor is what its CONTENT needs, which is the newest of the classes
    /// and the public C# members it writes. Either alone is not enough: a markup example
    /// mentions no C# and a <c>.txt</c> snippet of <c>ISednaUi</c> calls mentions no
    /// classes.
    /// </para>
    /// <para>
    /// Falling back to <see cref="LatestRelease"/> when an example had neither is what
    /// this replaces, and it was the bug: every JavaScript and C# snippet in the
    /// catalogue reported itself as first shipping in the newest release, so an agent on
    /// the previous one was warned off capabilities it already had. The fallback is now
    /// the release the example has looked exactly like since, which
    /// <c>build/class-history.sh</c> derives by comparing each tag's copy byte for byte.
    /// </para>
    /// </remarks>
    public string? SinceExample(string id, IEnumerable<string> classes, IEnumerable<string> members)
    {
        string? newest = null;
        var any = false;

        foreach (var name in classes)
        {
            var key = name.TrimStart('.');
            if (!_embedded.Classes.ContainsKey(key)) continue;
            any = true;
            var since = Since(m => m.Classes, key);
            // A single unreleased class makes the whole example unreleased.
            if (since is null) return null;
            if (newest is null || Compare(since, newest) > 0) newest = since;
        }

        foreach (var name in members)
        {
            if (!KnowsMember(name)) continue;
            var since = SinceMember(name);
            any = true;
            if (since is null) return null;
            if (newest is null || Compare(since, newest) > 0) newest = since;
        }

        return any ? newest : Since(m => m.Examples, id);
    }

    /// <summary>
    /// The embedded copy's answer, and where that is null — in no release this checkout
    /// knew of — the latest release's own answer, which is exact for the release it was
    /// generated at. A name the embedded copy lacks is null either way.
    /// </summary>
    private string? Since(Func<HistoryMaps, Dictionary<string, string?>> map, string key)
    {
        if (!map(_embedded).TryGetValue(key, out var embedded)) return null;
        if (embedded is not null) return embedded;

        var released = _released.Current;
        return released is not null && map(released).TryGetValue(key, out var atRelease) ? atRelease : null;
    }

    /// <summary>
    /// Builds the envelope, naming anything the caller's installed version does not
    /// have.
    /// </summary>
    public Meta For(string? installedVersion, IEnumerable<(string Name, string? Since)>? items = null)
    {
        // Rejected rather than ignored. A version this cannot parse compares as
        // 0.0.0, and the caller gets a confident warning built from nonsense —
        // ".btn is not in not-a-version" — while a typo'd version would silently
        // stop protecting the one thing this envelope exists to protect.
        if (installedVersion is not null && !Versionish.IsMatch(installedVersion))
            throw new McpException(
                $"installedVersion \"{installedVersion}\" is not a version. Pass the "
                + "Sedna.UI version your app has pinned, e.g. \"0.3.0\", or omit it.");

        string? warning = null;

        if (installedVersion is not null && items is not null)
        {
            // Two different answers, and conflating them sends the caller nowhere.
            // "Not in your version" has an upgrade behind it. "In no release" does
            // not — nothing published ships it, so telling that caller to upgrade is
            // advice they cannot take.
            var named = items
                .Where(i => i.Since is null || Compare(i.Since, installedVersion) > 0)
                .GroupBy(i => i.Name, StringComparer.Ordinal)
                .Select(g => (Name: g.Key, Unreleased: g.All(i => i.Since is null)))
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .ToList();

            var newer = named.Where(i => !i.Unreleased).Select(i => i.Name).ToList();
            var unreleased = named.Where(i => i.Unreleased).Select(i => i.Name).ToList();

            var parts = new List<string>(2);

            if (newer.Count > 0)
            {
                parts.Add(
                    $"{newer.Count} of these are not in {installedVersion}: "
                    + Name(newer)
                    + ". Upgrade Sedna.UI, or use something else.");
            }

            if (unreleased.Count > 0)
            {
                parts.Add(
                    $"{unreleased.Count} of these are in no release at all — no "
                    + "published version ships them: "
                    + Name(unreleased)
                    + ". There is nothing to upgrade to; use something else until they ship.");
            }

            if (parts.Count > 0) warning = string.Join(" ", parts);
        }

        return new Meta("main", Commit, BuiltUtc, LatestRelease, installedVersion, warning);
    }

    /// <summary>Names the first twelve and says there are more.</summary>
    private static string Name(IReadOnlyList<string> names) =>
        string.Join(", ", names.Take(12)) + (names.Count > 12 ? ", …" : string.Empty);

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

    /// <summary>What counts as a version: <c>1</c>, <c>1.2</c>, <c>1.2.3</c>, with an
    /// optional <c>v</c>, pre-release or build metadata.</summary>
    private static readonly Regex Versionish = new(
        @"^v?\d+(\.\d+){0,2}(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static int[] Parse(string version)
    {
        var core = version.TrimStart('v', 'V').Split('-')[0].Split('+')[0].Split('.');
        var parts = new int[3];
        for (var i = 0; i < 3; i++)
            parts[i] = i < core.Length && int.TryParse(core[i], out var n) ? n : 0;

        return parts;
    }
}
