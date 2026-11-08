using System.ComponentModel;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Navigation;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// The three tools an agent reaches for around the write-a-page loop: checking what it
/// wrote, finding out what changed between two releases, and reading the C# and script
/// API member by member.
/// </summary>
/// <remarks>
/// Read-only like the rest, for the same reason: the endpoint is public and
/// unauthenticated, and a client honouring the read-only hint calls these without
/// prompting. <b>There must never be a tool that writes.</b>
/// </remarks>
[McpServerToolType]
internal sealed class ReferenceTools(CatalogueIndex index, VersionEnvelope versions, ApiReference api)
{
    private const int MaxMarkupLength = 64 * 1024;
    private const int MaxApiNames = 10;
    private const int MaxListed = 400;

    private static readonly string[] Kinds = ["classes", "tokens", "csharp"];

    // ── check_markup ────────────────────────────────────────────────────────

    private static readonly Regex ClassAttribute = new(
        @"\bclass\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [McpServerTool(Name = "check_markup", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("""
        Check markup you wrote against the library: every class it uses that the stylesheet does
        not declare (with the nearest library class, in case it is a typo), every class a release
        renamed (with what replaced it), and every class newer than your installed version. Call
        before you hand over a page. Classes only — it does not judge structure.
        """)]
    public object CheckMarkup(
        [Description("The HTML or Razor markup to check, up to 64 KB.")]
        string markup,
        [Description("The Sedna.UI version your app has installed, e.g. \"0.30.0\". "
                     + "Supplying it also reports classes that version does not have.")]
        string? installedVersion = null)
    {
        if (string.IsNullOrWhiteSpace(markup))
            throw new McpException("markup is required: the HTML or Razor markup to check.");
        if (markup.Length > MaxMarkupLength)
            throw new McpException(
                $"markup is {markup.Length / 1024} KB; the limit is {MaxMarkupLength / 1024} KB. Check one page or one component at a time.");

        var used = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var dynamic = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match attribute in ClassAttribute.Matches(markup))
        {
            foreach (var token in attribute.Groups["v"].Value.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                // Razor that builds a class at render time cannot be checked from the source.
                if (token.Contains('@', StringComparison.Ordinal) || token.Contains('{', StringComparison.Ordinal)
                    || token.Contains('(', StringComparison.Ordinal))
                {
                    dynamic.Add(token);
                    continue;
                }
                used[token] = used.GetValueOrDefault(token) + 1;
            }
        }

        // Validated before anything is compared against it.
        versions.For(installedVersion);
        bool InstalledHas(string release) =>
            installedVersion is not null && VersionEnvelope.Compare(installedVersion, release) < 0;
        var gone = versions.Gone("classes").ToDictionary(g => g.Name, StringComparer.Ordinal);

        var unknown = new List<object>();
        var renamed = new List<object>();
        var later = new List<object>();
        var icons = new List<string>();
        var known = new List<(string Name, string? Since)>();

        foreach (var (name, count) in used)
        {
            if (name.StartsWith("ri-", StringComparison.Ordinal))
            {
                if (!index.Icons.Contains(name)) icons.Add(name);
                continue;
            }

            if (index.Class(name) is not null)
            {
                known.Add(("." + name, versions.SinceClass(name)));
                continue;
            }

            var rename = Renames.Of(name);
            var removal = gone.TryGetValue(name, out var g) ? g : default;

            // The installed version still has it, so it is right for this app today; it
            // only has to change on the upgrade that renamed or removed it.
            var stillThere = rename is not null ? InstalledHas(rename.Release)
                : removal.Name is not null && removal.Removed is { } removedIn
                  && VersionEnvelope.Compare(installedVersion ?? "0.0.0", removal.Since) >= 0 && InstalledHas(removedIn);
            if (stillThere)
            {
                later.Add(new
                {
                    name = "." + name,
                    changesIn = rename?.Release ?? removal.Removed,
                    use = rename?.New.Select(n => "." + n) ?? Enumerable.Empty<string>(),
                    count,
                });
                continue;
            }

            if (rename is not null)
            {
                renamed.Add(new { name = "." + name, use = rename.New.Select(n => "." + n), since = rename.Release, count });
                continue;
            }

            unknown.Add(new
            {
                name = "." + name,
                count,
                removedIn = removal.Name is null ? null : removal.Removed ?? "unreleased",
                nearest = Nearest(name, index.Classes.Select(c => c.Name)).Select(n => "." + n),
            });
        }

        List<object> tooNew = installedVersion is null
            ? []
            : known.Where(k => k.Since is null || VersionEnvelope.Compare(k.Since, installedVersion) > 0)
                .Select(k => (object)new { name = k.Name, since = k.Since ?? "unreleased" }).ToList();

        return new
        {
            meta = versions.For(installedVersion, known),
            checkedClasses = used.Count,
            libraryClasses = known.Count,
            unknown,
            renamed,
            notInInstalledVersion = tooNew,
            changesOnUpgrade = later,
            unknownIcons = icons,
            dynamicClasses = dynamic,
            ok = unknown.Count == 0 && renamed.Count == 0 && tooNew.Count == 0 && icons.Count == 0,
            note = "An unknown class is either a typo of a library class or your app's own — an app's own "
                   + "class is prefixed with the app's name, and the library never declares it. Replace a "
                   + "renamed class with every class it lists, never by its prefix. changesOnUpgrade is "
                   + "right for your installed version and changes in the release it names.",
        };
    }

    /// <summary>The three library names closest to an unknown one, or none worth suggesting.</summary>
    private static IEnumerable<string> Nearest(string name, IEnumerable<string> candidates)
    {
        var limit = Math.Max(2, name.Length / 4);
        return candidates
            .Select(c => (Name: c, Distance: Distance(name, c)))
            .Where(c => c.Distance <= limit)
            .OrderBy(c => c.Distance).ThenBy(c => c.Name, StringComparer.Ordinal)
            .Take(3)
            .Select(c => c.Name);
    }

    private static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 8) return int.MaxValue;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    // ── get_changes ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_changes", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("""
        What changed between two releases — the upgrade diff: every class, token and public C#
        member added and removed, every class renamed (old name to the classes that look the
        same), and the releases in between with a link to each one's notes, where the breaking
        changes are listed first. Call before upgrading an app from one version to another.
        """)]
    public object GetChanges(
        [Description("The version the app has now, e.g. \"0.22.0\".")]
        string from,
        [Description("The version to upgrade to, e.g. \"0.30.0\". Omit for the latest release; "
                     + "\"main\" includes what is merged and in no release yet.")]
        string? to = null,
        [Description("Restrict to one of: classes, tokens, csharp. Omit for all three.")]
        string? kind = null)
    {
        if (string.IsNullOrWhiteSpace(from))
            throw new McpException("from is required: the version the app has now, e.g. \"0.22.0\".");
        if (kind is not null && !Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
            throw new McpException($"Unknown kind \"{kind}\". Use one of: {string.Join(", ", Kinds)} — or omit it for all three.");

        // For the version check only: an unparseable version is rejected there.
        versions.For(from);
        var main = string.Equals(to, "main", StringComparison.OrdinalIgnoreCase);
        var target = main || to is null ? versions.LatestRelease : to;
        versions.For(target);

        if (VersionEnvelope.Compare(from, target) > 0)
            throw new McpException($"from ({from}) is newer than to ({target}). Pass the older version as from.");

        bool InRange(string? version) =>
            version is null ? main : VersionEnvelope.Compare(version, from) > 0 && VersionEnvelope.Compare(version, target) <= 0;

        string[] kinds = kind is null ? Kinds : [kind.ToLowerInvariant()];
        var added = new Dictionary<string, object>(StringComparer.Ordinal);
        var removed = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var k in kinds)
        {
            var prefix = k == "classes" ? "." : "";
            var newNames = versions.Current(k).Where(n => InRange(n.Since))
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .Select(n => new { name = prefix + n.Name, since = n.Since ?? "unreleased" })
                .ToList();
            // Gone by the target, and there in the version the app has.
            var goneNames = versions.Gone(k)
                .Where(n => VersionEnvelope.Compare(n.Since, from) <= 0 && InRange(n.Removed))
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .Select(n => new { name = prefix + n.Name, removed = n.Removed ?? "unreleased" })
                .ToList();

            added[k] = new { count = newNames.Count, names = newNames.Take(MaxListed), truncated = newNames.Count > MaxListed };
            removed[k] = new { count = goneNames.Count, names = goneNames.Take(MaxListed), truncated = goneNames.Count > MaxListed };
        }

        List<object> renames = kinds.Contains("classes")
            ? Renames.All.Where(m => InRange(m.Release)).SelectMany(m => m.Renames)
                .Select(r => (object)new { name = "." + r.Old, use = r.New.Select(n => "." + n), since = r.Release })
                .ToList()
            : [];

        var releases = Released()
            .Where(v => VersionEnvelope.Compare(v, from) > 0 && VersionEnvelope.Compare(v, target) <= 0)
            .Select(v => new { version = v, notes = $"{CatalogueLinks.Releases}/tag/v{v}" })
            .ToList();

        return new
        {
            meta = versions.For(null),
            from,
            to = main ? "main" : target,
            releases,
            added,
            removed,
            renamed = renames,
            note = "A removed class is the breaking part of an upgrade: grep the app for each one. A renamed "
                   + "class becomes every class it lists. Each release's notes list its other breaking changes "
                   + "first — a rule whose values moved — and only the notes say those.",
        };
    }

    /// <summary>Every release the history knows of, oldest first.</summary>
    private IEnumerable<string> Released() =>
        Kinds.SelectMany(k => versions.Current(k).Select(n => n.Since)
                .Concat(versions.Gone(k).SelectMany(g => new[] { g.Since, g.Removed })))
            .Append(versions.LatestRelease)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(Comparer<string>.Create(VersionEnvelope.Compare));

    // ── describe_api ────────────────────────────────────────────────────────

    [McpServerTool(Name = "describe_api", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("""
        The C# and JavaScript API, member by member: a type's members, a member's signatures,
        its documentation, the release it first shipped in and the examples that use it. Takes
        C# names — ISednaUi, ISednaUi.ShowModalAsync, or a bare ShowModalAsync — and members of
        the sednaUi script global — sednaUi.toast or toast.show.
        """)]
    public object DescribeApi(
        [Description("API names, at most 10, e.g. [\"ISednaOverlays.ShowAsync\", \"sednaUi.modal\"].")]
        string[] names,
        [Description("The Sedna.UI version your app has installed.")]
        string? installedVersion = null)
    {
        if (names is null || names.Length == 0)
            throw new McpException("names is required: one or more API names, e.g. [\"ISednaUi.ShowModalAsync\"].");
        if (names.Length > MaxApiNames)
            throw new McpException($"At most {MaxApiNames} names per call; {names.Length} were requested. Split them across calls.");

        var csharp = new List<object>();
        var script = new List<object>();
        var missing = new List<object>();
        var reported = new List<(string, string?)>();

        foreach (var raw in names)
        {
            var name = raw.Trim();
            var isScript = name.StartsWith("sednaUi.", StringComparison.Ordinal) || (name.Length > 0 && char.IsLower(name[0]));

            if (isScript && api.ScriptRow(name) is { } row)
            {
                script.Add(new
                {
                    name = "sednaUi." + string.Join(", sednaUi.", row.Names),
                    asked = name,
                    purpose = row.Purpose,
                    usedByExamples = index.Examples
                        .Where(e => row.Names.Any(n => e.Markup.Contains("sednaUi." + n, StringComparison.Ordinal)))
                        .Select(e => e.Id).ToList(),
                });
                continue;
            }

            IReadOnlyList<ApiMember> members = isScript ? [] : api.Csharp(name);
            if (members.Count == 0)
            {
                missing.Add(new
                {
                    name,
                    nearest = Nearest(name, api.AllNames()),
                    note = isScript
                        ? "No member of the sednaUi global by that name. docs/architecture.md lists them all."
                        : "The library exports no type or member by that name.",
                });
                continue;
            }

            foreach (var member in members)
            {
                var since = versions.SinceMember(member.Name);
                reported.Add((member.Name, since));
                var bare = member.Name.Contains('.', StringComparison.Ordinal) ? member.Name[(member.Name.IndexOf('.', StringComparison.Ordinal) + 1)..] : member.Name;
                csharp.Add(new
                {
                    name = member.Name,
                    kind = member.Kind,
                    signatures = member.Signatures,
                    summary = member.Summary,
                    parameters = member.Parameters,
                    returns = member.Returns,
                    since = since ?? "unreleased",
                    usedByExamples = index.Examples
                        .Where(e => e.Members.Contains(member.Name, StringComparer.Ordinal)
                                    || e.Members.Contains(bare, StringComparer.Ordinal))
                        .Select(e => e.Id).ToList(),
                });
            }
        }

        return new
        {
            meta = versions.For(installedVersion, reported),
            csharp,
            script,
            notFound = missing,
        };
    }
}
