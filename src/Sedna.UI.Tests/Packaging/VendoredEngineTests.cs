using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every vendored engine: what is there, under which licence, where it came from, and that the
/// notice a reader audits agrees with it.
/// </summary>
/// <remarks>
/// Each <c>build/vendor-*.sh</c> writes one <c>wwwroot/lib/&lt;engine&gt;/</c> and its <c>VENDORED.txt</c>,
/// through the shared <c>build/vendor-npm.py</c>; these hold the committed files to what that promises,
/// so a file dropped in by hand — a newer build copied from a CDN, a copyleft one — fails here rather
/// than shipping. The registry test holds the same engines to the surface contract.
/// </remarks>
public class VendoredEngineTests
{
    public static TheoryData<string, string> Engines() => new()
    {
        { "cytoscape", "build/vendor-cytoscape.sh" },
        { "quill", "build/vendor-quill.sh" },
    };

    private static string LibDir(string engine) => Path.Combine(Assets.ProjectDir, "wwwroot", "lib", engine);

    private sealed record Vendored(string Name, string Version, string Integrity, string[] Files);

    private static List<Vendored> Manifest(string engine) =>
        File.ReadAllLines(Path.Combine(LibDir(engine), "VENDORED.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split(' ', 4))
            .Select(p => new Vendored(p[0], p[1], p[2],
                p[3].StartsWith('(') ? [] : p[3].Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .ToList();

    private static string LicenceFile(string engine, string package) =>
        Path.Combine(LibDir(engine), "licenses", package.TrimStart('@').Replace('/', '-') + ".txt");

    // The permissive licences the vendoring engine accepts, by a phrase only their text has.
    private static readonly string[][] Permissive =
    [
        ["Permission is hereby granted, free of charge"],
        ["Redistribution and use in source and binary forms", "Neither the name"],
        ["Apache License", "Version 2.0"],
    ];

    [Fact]
    public void Every_engine_under_lib_is_one_of_these()
    {
        // A directory under lib/ with a manifest and no test here would ship unchecked.
        var engines = Engines().Select(row => (string)row[0]).ToHashSet(StringComparer.Ordinal);
        var onDisk = Directory.GetDirectories(Path.Combine(Assets.ProjectDir, "wwwroot", "lib"))
            .Where(d => File.Exists(Path.Combine(d, "VENDORED.txt")))
            .Select(Path.GetFileName).OfType<string>();
        Assert.All(onDisk, d => Assert.Contains(d, engines));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Every_vendored_package_ships_its_own_licence_and_it_is_permissive(string engine, string script)
    {
        // Each licence's condition on redistribution is that the notice travels with the files. And a
        // copyleft licence would pass its terms to every app that installs the package — which is why
        // cytoscape-svg, GPL-3.0, is not here.
        _ = script;
        var problems = new List<string>();
        foreach (var package in Manifest(engine))
        {
            var path = LicenceFile(engine, package.Name);
            if (!File.Exists(path))
            {
                problems.Add($"{package.Name}: no licence at {Path.GetFileName(path)}");
                continue;
            }

            var text = File.ReadAllText(path);
            if (!Permissive.Any(signs => signs.All(s => text.Contains(s, StringComparison.Ordinal))))
                problems.Add($"{package.Name}: its licence is not MIT, BSD-3-Clause or Apache-2.0 text");
            if (Regex.IsMatch(text, @"GNU (Lesser |Affero )?General Public License|Mozilla Public License|Eclipse Public License", RegexOptions.IgnoreCase))
                problems.Add($"{package.Name}: copyleft licence");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void The_manifest_lists_exactly_the_files_that_ship(string engine, string script)
    {
        // A script file the manifest does not list came from somewhere other than the vendoring
        // script, with no pinned version and no checksum behind it.
        _ = script;
        var listed = Manifest(engine).SelectMany(p => p.Files).OrderBy(f => f, StringComparer.Ordinal).ToList();
        var onDisk = Directory.GetFiles(LibDir(engine), "*.js").Select(Path.GetFileName).OfType<string>()
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

        Assert.Equal(listed, onDisk);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Every_vendored_file_names_its_source_and_imports_only_its_neighbours(string engine, string script)
    {
        // The provenance header is what lets a reader of the package — or of a browser's devtools —
        // find out what a file is without this repository. And a bare specifier ("lodash.throttle")
        // would need an import map no consuming app has: every import is a file beside it.
        var problems = new List<string>();
        foreach (var package in Manifest(engine))
        {
            foreach (var file in package.Files)
            {
                var text = File.ReadAllText(Path.Combine(LibDir(engine), file));
                var head = text[..Math.Min(text.Length, 600)];
                if (!head.Contains($"{package.Name} {package.Version}", StringComparison.Ordinal)
                    || !head.Contains(script, StringComparison.Ordinal))
                    problems.Add($"{file}: no provenance header naming {package.Name} {package.Version} and {script}");

                foreach (Match m in Regex.Matches(text, @"(?:\bfrom\s*|\bimport\s*\(?\s*)(['""])([^'""]+)\1"))
                {
                    var spec = m.Groups[2].Value;
                    if (!spec.StartsWith("./", StringComparison.Ordinal))
                        problems.Add($"{file}: imports '{spec}', which is not a file beside it");
                    else if (!File.Exists(Path.Combine(LibDir(engine), spec[2..])))
                        problems.Add($"{file}: imports '{spec}', which does not ship");
                }

                if (text.Contains('\r', StringComparison.Ordinal))
                    problems.Add($"{file}: CRLF line endings — the vendoring script writes LF");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void The_notices_file_names_every_vendored_package_at_its_version(string engine, string script)
    {
        // THIRD-PARTY-NOTICES.md is what somebody auditing the package reads, and VENDORED.txt is
        // what actually ships. A version bumped in one and not the other is a notice for a file
        // that is not in the package.
        _ = script;
        var notices = File.ReadAllText(Path.Combine(Assets.RepoRoot, "THIRD-PARTY-NOTICES.md"));
        var missing = Manifest(engine)
            .Where(p => !Regex.IsMatch(notices, $@"\|\s*{Regex.Escape(p.Name)}\s*\|\s*{Regex.Escape(p.Version)}\s*\|"))
            .Select(p => $"{p.Name} {p.Version}")
            .ToList();

        Assert.True(missing.Count == 0,
            "THIRD-PARTY-NOTICES.md does not list these at the vendored version: " + string.Join(", ", missing));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Every_integrity_is_a_pinned_sha512(string engine, string script)
    {
        _ = script;
        foreach (var package in Manifest(engine))
            Assert.Matches(@"^sha512-[A-Za-z0-9+/]{86}==$", package.Integrity);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void The_vendoring_script_writes_this_engine(string engine, string script)
    {
        var text = File.ReadAllText(Path.Combine(Assets.RepoRoot, script));
        Assert.Contains($"src/Sedna.UI/wwwroot/lib/{engine}", text, StringComparison.Ordinal);
        Assert.Contains("vendor-npm.py", text, StringComparison.Ordinal);
    }
}
