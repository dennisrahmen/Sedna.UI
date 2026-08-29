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
    private readonly Dictionary<string, string?> _csharp;
    private readonly Dictionary<string, string?> _examples;
    private readonly Dictionary<string, string?> _byMemberName;

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
        _csharp = Map(root.GetProperty("csharp"));
        _examples = Map(root.GetProperty("examples"));

        // An example mentions `RegisterCommandsAsync`, not `ISednaUi.RegisterCommandsAsync`,
        // so the bare name has to resolve too. Where two types declare the same member —
        // `Href` is on both PaletteCommand and SearchItem — the OLDEST wins: the question
        // this answers is "can my version write this", and either type having had it since
        // 0.2.0 makes the answer yes.
        _byMemberName = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, since) in _csharp)
        {
            var dot = key.IndexOf('.', StringComparison.Ordinal);
            var bare = dot < 0 ? key : key[(dot + 1)..];
            if (!_byMemberName.TryGetValue(bare, out var held))
            {
                _byMemberName[bare] = since;
                continue;
            }

            if (held is null || (since is not null && Compare(since, held) < 0))
                _byMemberName[bare] = since;
        }

        Commit = ResolveCommit(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion,
            Environment.GetEnvironmentVariable);

        BuiltUtc = File.GetLastWriteTimeUtc(assembly.Location).ToString("O");
    }

    public string LatestRelease { get; }

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
    public string? SinceClass(string name) => _classes.GetValueOrDefault(name.TrimStart('.'));

    /// <summary>The release a token first shipped in, or null if it is unreleased.</summary>
    public string? SinceToken(string name) => _tokens.GetValueOrDefault(name);

    /// <summary>The release a public C# type or member first shipped in.</summary>
    /// <remarks>
    /// Accepts either <c>ISednaUi.ToastAsync</c> or the bare <c>ToastAsync</c>. Returns
    /// null for an unreleased member and, deliberately, also for a name the library does
    /// not declare — the caller only ever asks about names the index matched.
    /// </remarks>
    public string? SinceMember(string name) =>
        _csharp.TryGetValue(name, out var exact) ? exact : _byMemberName.GetValueOrDefault(name);

    /// <summary>Whether a name is one the C# history knows.</summary>
    public bool KnowsMember(string name) => _csharp.ContainsKey(name) || _byMemberName.ContainsKey(name);

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
            if (!_classes.TryGetValue(name.TrimStart('.'), out var since)) continue;
            any = true;
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

        return any ? newest : _examples.GetValueOrDefault(id);
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

    private static Dictionary<string, string?> Map(JsonElement element) =>
        element.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString(),
            StringComparer.Ordinal);
}
