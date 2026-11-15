using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The browser floor is current Chromium and current Safari on iOS. A pull request, every push to
/// main and a release tag each run the suite through one reusable workflow, <c>tests.yml</c>, which
/// runs every test project in every engine; a release publishes only when that suite is green.
/// </summary>
/// <remarks>
/// The engines are <see cref="BrowserEngine.All"/> and the test projects are every
/// <c>*.Tests</c> directory under <c>src/</c>. An engine, a project or a shard that the
/// matrix does not name would go untested while the build stayed green, so each is checked here
/// rather than remembered. The matrix is built by the <c>plan</c> job's script, so these tests run
/// that script and read the matrix it prints, not a copy of it.
/// </remarks>
public class BrowserFloorTests
{
    private static string Workflow(string name) => WorkflowYaml.Read(name);

    private const string Suite = "tests.yml";

    private static readonly string[] Callers = ["ci.yml", "main.yml", "release.yml"];

    [Fact]
    public void The_suite_tests_every_engine()
    {
        var slices = WorkflowYaml.Matrix(Workflow(Suite));
        var engines = slices.Select(s => s.Engine).Distinct().ToList();
        foreach (var engine in BrowserEngine.All)
            Assert.True(engines.Contains(engine), $"{Suite}'s plan job puts no slice in {engine}. Add it to the engines the matrix is built for.");
        Assert.True(engines.All(e => BrowserEngine.All.Contains(e)),
            $"{Suite}'s plan job names an engine the floor does not cover: {string.Join(", ", engines.Except(BrowserEngine.All))}.");

        // Every engine runs the same slices, or one would be tested less than the other.
        var bySlice = slices.GroupBy(s => s.Slug.Substring(s.Slug.IndexOf('-', StringComparison.Ordinal) + 1))
            .Where(g => g.Select(s => s.Engine).Distinct().Count() != BrowserEngine.All.Length)
            .Select(g => g.Key).ToList();
        Assert.True(bySlice.Count == 0, $"{Suite}: these slices do not run in every engine: {string.Join(", ", bySlice)}.");
    }

    [Fact]
    public void The_suite_tests_every_test_project()
    {
        var projects = WorkflowYaml.Matrix(Workflow(Suite)).Select(s => s.Project).Distinct().ToList();
        var expected = Directory.GetDirectories(Path.Combine(Assets.RepoRoot, "src"), "*.Tests")
            .Select(d => "src/" + Path.GetFileName(d))
            .ToList();

        Assert.NotEmpty(expected);
        foreach (var project in expected)
            Assert.True(projects.Contains(project), $"{Suite}'s matrix has no slice for {project}. Add one to the plan job's slices.");
        foreach (var project in projects)
            Assert.True(expected.Contains(project), $"{Suite}'s matrix runs {project}, which is no test project under src/.");
    }

    /// <summary>
    /// The catalogue's tests are cut into slices by their <c>Kind</c> trait: everything that is not
    /// a page scan, once, and the scans in shards, <c>SEDNA_UI_SHARD=k/n</c> for every k from 1 to n.
    /// A shard missing from the matrix is a share of the scans nobody runs.
    /// </summary>
    [Fact]
    public void The_catalogue_slices_leave_nothing_between_them()
    {
        var yaml = Workflow(Suite);
        var catalogue = WorkflowYaml.Matrix(yaml).Where(s => s.Project.EndsWith("Catalogue.Tests", StringComparison.Ordinal)).ToList();

        foreach (var engine in BrowserEngine.All)
        {
            var slices = catalogue.Where(s => s.Engine == engine).ToList();
            var rest = slices.Where(s => s.Filter == "Kind!=Scan").ToList();
            Assert.True(rest.Count == 1 && rest[0].Shard == "",
                $"{Suite}: in {engine} the catalogue's tests that are not scans must be exactly one slice with filter `Kind!=Scan` and no shard; there are {rest.Count}.");

            var scans = slices.Where(s => s.Filter == "Kind=Scan").ToList();
            Assert.True(scans.Count > 0, $"{Suite}: in {engine} no slice runs the catalogue's page scans (`Kind=Scan`).");
            var parts = scans.Select(s => Regex.Match(s.Shard, @"^(?<k>\d+)/(?<n>\d+)$")).ToList();
            Assert.True(parts.All(p => p.Success), $"{Suite}: a `Kind=Scan` slice has no shard of the form k/n: {string.Join(", ", scans.Select(s => $"'{s.Shard}'"))}.");
            var counts = parts.Select(p => int.Parse(p.Groups["n"].Value)).Distinct().ToList();
            Assert.True(counts.Count == 1, $"{Suite}: the scan shards of {engine} disagree on how many there are: {string.Join(", ", counts)}.");
            var shards = parts.Select(p => int.Parse(p.Groups["k"].Value)).Order().ToList();
            Assert.True(shards.SequenceEqual(Enumerable.Range(1, counts[0])),
                $"{Suite}: {engine} runs scan shards {string.Join(",", shards)} of {counts[0]}; every shard from 1 to {counts[0]} must run exactly once.");

            // Nothing in the catalogue is left out: every slice of it is one of the two kinds.
            Assert.True(slices.Count == rest.Count + scans.Count,
                $"{Suite}: {engine} has a catalogue slice that is neither `Kind!=Scan` nor `Kind=Scan`, so it overlaps or leaves tests out.");
        }

        // The shard reaches the tests: exported from the slice's own value, for a slice that has one.
        Assert.Matches(@"SLICE_SHARD:\s*\$\{\{ matrix\.shard \}\}", yaml);
        Assert.Matches(@"export SEDNA_UI_SHARD=""\$SLICE_SHARD""", yaml);
        Assert.Matches(@"SLICE_FILTER:\s*\$\{\{ matrix\.filter \}\}", yaml);
        Assert.Contains("--filter \"$FILTER\"", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void The_suite_runs_the_browser_tests_in_the_matrix_engine()
    {
        var yaml = Workflow(Suite);
        Assert.Matches(@"SEDNA_UI_BROWSER_TESTS:\s*'1'", yaml);
        Assert.Matches(@"SEDNA_UI_BROWSER:\s*\$\{\{ matrix\.engine \}\}", yaml);
    }

    /// <summary>
    /// The website's own tests (<c>Scope=Site</c>) check the website, not the package. The release
    /// leaves exactly those out, and every push to main and every pull request runs them, so none
    /// is lost: the three callers differ in one input, and <c>tests.yml</c> turns it into a filter.
    /// </summary>
    [Fact]
    public void The_website_tests_leave_the_release_and_run_on_main()
    {
        Assert.Equal("false", WorkflowYaml.SiteInput("release.yml"));
        Assert.Equal("true", WorkflowYaml.SiteInput("main.yml"));
        Assert.Equal("true", WorkflowYaml.SiteInput("ci.yml"));

        var yaml = Workflow(Suite);
        Assert.Matches(@"if \[\[ ""\$SITE"" != 'true' \]\]; then FILTER=""\$\{FILTER:\+\$FILTER&\}Scope!=Site""; fi", yaml);
        Assert.Matches(@"SITE:\s*\$\{\{ inputs\.site \}\}", yaml);
        Assert.Matches(@"(?s)workflow_call:.*?site:.*?default: true", yaml);
        Assert.Contains("branches: [main]", Workflow("main.yml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_caller_runs_the_same_suite()
    {
        foreach (var caller in Callers)
        {
            var calls = WorkflowYaml.Jobs(Workflow(caller))
                .Where(j => Regex.IsMatch(j.Value, @"^    uses:\s*\./\.github/workflows/tests\.yml\s*$", RegexOptions.Multiline))
                .ToList();
            Assert.True(calls.Count == 1, $"{caller} must call ./.github/workflows/tests.yml in exactly one job; it calls it in {calls.Count}.");
        }

        // `ready` is the one check the ruleset requires: it waits for every other job of ci.yml
        // and runs whatever they did, so a job that is added and not waited for cannot go unseen.
        var ci = WorkflowYaml.Jobs(Workflow("ci.yml"));
        Assert.True(ci.TryGetValue("ready", out var ready), "ci.yml has no `ready` job, the one check the ruleset requires.");
        var needs = WorkflowYaml.Needs(ready!);
        var others = ci.Keys.Where(k => k != "ready").Order().ToList();
        Assert.True(others.SequenceEqual(needs.Order()),
            $"ci.yml's `ready` must need every other job ({string.Join(", ", others)}); it needs {string.Join(", ", needs)}.");
        Assert.Matches(@"^    if:\s*\$\{\{\s*always\(\)\s*\}\}\s*$", string.Join("\n", ready!.Split('\n').Where(l => l.StartsWith("    if:", StringComparison.Ordinal))));
    }

    /// <summary>
    /// A release publishes only after the full suite. The suite is a call of <c>tests.yml</c> that
    /// is skipped only when main.yml already ran it on this exact commit, and the release job runs
    /// only when the suite succeeded or was skipped for that reason.
    /// </summary>
    [Fact]
    public void Nothing_publishes_before_the_full_suite_is_green()
    {
        var jobs = WorkflowYaml.Jobs(Workflow("release.yml"));
        const string missing = "release.yml needs a `preflight`, a `tests` and a `release` job.";
        Assert.True(jobs.ContainsKey("preflight"), missing);
        Assert.True(jobs.TryGetValue("release", out var release), missing);
        Assert.True(jobs.TryGetValue("tests", out var tests), missing);
        Assert.NotNull(release);
        Assert.NotNull(tests);

        var needs = WorkflowYaml.Needs(release);
        Assert.True(needs.Contains("preflight") && needs.Contains("tests"),
            $"release.yml's release job must need both `preflight` and `tests`; it needs {string.Join(", ", needs)}.");

        var condition = Regex.Replace(WorkflowYaml.If(release), @"\s+", " ");
        Assert.True(condition.Length > 0, "release.yml's release job has no `if:`; it would run after a failed or skipped suite.");
        Assert.DoesNotContain("always()", condition, StringComparison.Ordinal);
        Assert.Contains("needs.preflight.result == 'success'", condition, StringComparison.Ordinal);
        Assert.Contains("needs.tests.result == 'success'", condition, StringComparison.Ordinal);
        Assert.Contains("needs.tests.result == 'skipped' && needs.preflight.outputs.verified == 'true'", condition, StringComparison.Ordinal);
        Assert.True(Regex.Matches(condition, @"needs\.tests\.result").Count == 2,
            $"release.yml's release `if:` may name the suite's result only as succeeded, or as skipped with the commit verified: {condition}");

        Assert.Matches(@"^    uses:\s*\./\.github/workflows/tests\.yml\s*$", string.Join("\n", tests.Split('\n').Where(l => l.StartsWith("    uses:", StringComparison.Ordinal))));
        Assert.True(WorkflowYaml.Needs(tests).Contains("preflight"), "release.yml's tests job must need `preflight`, whose `verified` output it reads.");
        Assert.Equal("needs.preflight.outputs.verified != 'true'", Regex.Replace(WorkflowYaml.If(tests), @"\s+", " ").Trim());

        // verified is set from the run of main.yml, on branch main, for this commit — and from nothing else.
        var verified = Regex.Match(Workflow("release.yml"), @"- name: [^\n]*\n\s+id: verified\n(?<step>(?:.*\n)+?)(?=\n  \S|\n      - |\z)");
        Assert.True(verified.Success, "release.yml has no step with `id: verified`.");
        var step = verified.Groups["step"].Value;
        Assert.Contains("workflows/main.yml/runs", step, StringComparison.Ordinal);
        Assert.Contains("branch=main", step, StringComparison.Ordinal);
        Assert.Contains("head_sha=$GITHUB_SHA", step, StringComparison.Ordinal);
        Assert.Contains("*\" completed success\") VERIFIED=true", step, StringComparison.Ordinal);
        Assert.True(Regex.Matches(step, @"VERIFIED=true").Count == 1,
            "release.yml's `verified` step may set VERIFIED=true only for a completed, successful run of main.yml.");
    }

    /// <summary>
    /// The suite runs in Microsoft's Playwright image, which carries the browsers and their system
    /// libraries. Its tag has to be the Playwright package's version: the package drives the browser
    /// build it was released with, and an image of another version has other builds in it, which
    /// the package cannot launch.
    /// </summary>
    [Fact]
    public void The_suite_runs_in_the_Playwright_image_of_the_package_version()
    {
        var yaml = Workflow(Suite);
        var image = Regex.Match(yaml, @"container:\s*mcr\.microsoft\.com/playwright/dotnet:v(?<v>[\d.]+)-");
        Assert.True(image.Success, $"{Suite}'s test job runs in no Playwright image, so it would install the browsers and their libraries itself.");

        // In a container the checkout is another user's, and git refuses it until it is
        // marked safe; the class history reads the tags with git, before the build.
        var trust = yaml.IndexOf("safe.directory \"$GITHUB_WORKSPACE\"", image.Index, StringComparison.Ordinal);
        var history = yaml.IndexOf("build/class-history.sh", image.Index, StringComparison.Ordinal);
        Assert.True(trust > 0 && trust < history,
            $"{Suite}'s container test job does not mark the checkout safe before git reads it.");

        // The image carries no Python, and the app tooling's tests start python3.
        var python = Regex.Match(yaml, @"apt-get install[^\n]*\bpython3\b");
        var test = yaml.IndexOf("dotnet test", image.Index, StringComparison.Ordinal);
        Assert.True(python.Success && python.Index > image.Index && python.Index < test,
            $"{Suite}'s container test job does not install python3 before the tests, which start it.");

        var csprojs = Directory.GetFiles(Path.Combine(Assets.RepoRoot, "src"), "*.Tests.csproj", SearchOption.AllDirectories);
        var checkedAny = false;
        foreach (var csproj in csprojs)
        {
            var package = Regex.Match(File.ReadAllText(csproj), @"Include=""Microsoft\.Playwright"" Version=""(?<v>[\d.]+)""");
            if (!package.Success) continue;
            checkedAny = true;
            Assert.True(package.Groups["v"].Value == image.Groups["v"].Value,
                $"{Path.GetFileName(csproj)} uses Playwright {package.Groups["v"].Value} and {Suite}'s image is {image.Groups["v"].Value}. Move them together.");
        }

        Assert.True(checkedAny, "No test project references Microsoft.Playwright, so the image's version is compared with nothing.");
    }
}

/// <summary>
/// The workflows read as text, as far as the tests need: the jobs of a file, what a job needs,
/// its condition, and the matrix the <c>plan</c> job of <c>tests.yml</c> prints.
/// </summary>
internal static class WorkflowYaml
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(Assets.RepoRoot, ".github", "workflows", name));

    /// <summary>The jobs of a workflow by name, each as the text from its key to the next job.</summary>
    public static Dictionary<string, string> Jobs(string text)
    {
        var start = text.IndexOf("\njobs:", StringComparison.Ordinal);
        Assert.True(start >= 0, "A workflow has no jobs.");
        var body = text[(start + 1)..];
        var heads = Regex.Matches(body, @"^  (?<job>[a-z][a-z0-9_-]*):[ \t]*\r?$", RegexOptions.Multiline).ToList();
        var jobs = new Dictionary<string, string>();
        for (var i = 0; i < heads.Count; i++)
        {
            var to = i + 1 < heads.Count ? heads[i + 1].Index : body.Length;
            jobs[heads[i].Groups["job"].Value] = body[heads[i].Index..to];
        }

        return jobs;
    }

    /// <summary>The jobs a job waits for: `needs: x` or `needs: [x, y]`.</summary>
    public static List<string> Needs(string job)
    {
        var match = Regex.Match(job, @"^    needs:\s*(?:\[(?<list>[^\]]*)\]|(?<one>[\w-]+))\s*$", RegexOptions.Multiline);
        if (!match.Success) return [];
        var raw = match.Groups["list"].Success ? match.Groups["list"].Value : match.Groups["one"].Value;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>A job's `if:`, whether on one line or folded over several; empty when it has none.</summary>
    public static string If(string job)
    {
        var lines = job.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("    if:", StringComparison.Ordinal)) continue;
            var text = lines[i]["    if:".Length..].Trim();
            if (text is ">-" or ">" or "|")
            {
                var folded = new List<string>();
                for (var j = i + 1; j < lines.Length && lines[j].StartsWith("      ", StringComparison.Ordinal); j++)
                    folded.Add(lines[j].Trim());
                text = string.Join(" ", folded);
            }

            return text;
        }

        return "";
    }

    /// <summary>The `site:` a workflow passes to tests.yml.</summary>
    public static string SiteInput(string caller)
    {
        var call = Jobs(Read(caller)).Values
            .FirstOrDefault(j => Regex.IsMatch(j, @"^    uses:\s*\./\.github/workflows/tests\.yml\s*$", RegexOptions.Multiline));
        Assert.True(call is not null, $"{caller} does not call ./.github/workflows/tests.yml.");
        var site = Regex.Match(call, @"^      site:\s*(?<v>\S+)\s*$", RegexOptions.Multiline);
        Assert.True(site.Success, $"{caller} passes no `site:` to tests.yml.");
        return site.Groups["v"].Value;
    }

    public sealed record Slice(string Engine, string Project, string Filter, string Shard, string Slug, int Timeout);

    /// <summary>
    /// The matrix tests.yml's `plan` job prints, from running its script with the shard count it sets.
    /// </summary>
    public static IReadOnlyList<Slice> Matrix(string text)
    {
        var plan = Jobs(text)["plan"];
        var start = Regex.Match(plan, @"^( +)python3 - <<'PY'[^\n]*\n", RegexOptions.Multiline);
        Assert.True(start.Success, "tests.yml's plan job has no `python3 - <<'PY'` script that prints the matrix.");
        var lines = plan[(start.Index + start.Length)..].Split('\n');
        var end = Array.FindIndex(lines, l => l.Trim() == "PY");
        Assert.True(end > 0, "tests.yml's plan script has no closing PY line.");
        var indent = lines[0].Length - lines[0].TrimStart().Length;
        var script = string.Join("\n", lines.Take(end).Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));

        var shards = Regex.Match(plan, @"SHARDS:\s*'(?<n>\d+)'");
        Assert.True(shards.Success, "tests.yml's plan job sets no SHARDS.");

        // The script is run from a file, not piped in: a write to the interpreter's input fails
        // with a broken pipe whenever the interpreter has gone first, and hides why it went.
        var file = Path.Combine(Path.GetTempPath(), $"sedna-plan-{Guid.NewGuid():N}.py");
        File.WriteAllText(file, script);
        Task<string> stdout, stderr;
        try
        {
            var info = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.ArgumentList.Add("-I");
            info.ArgumentList.Add(file);
            info.Environment["SHARDS"] = shards.Groups["n"].Value;
            using var process = Process.Start(info)!;
            stdout = process.StandardOutput.ReadToEndAsync();
            stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(60_000), "tests.yml's plan script did not finish.");
            Assert.True(process.ExitCode == 0, $"tests.yml's plan script exited with {process.ExitCode}: {stderr.Result}");
        }
        finally
        {
            File.Delete(file);
        }

        var line = stdout.Result.Split('\n').FirstOrDefault(l => l.StartsWith("matrix=", StringComparison.Ordinal));
        Assert.True(line is not null, "tests.yml's plan script prints no `matrix=` line.");
        using var doc = JsonDocument.Parse(line["matrix=".Length..]);
        return doc.RootElement.GetProperty("include").EnumerateArray()
            .Select(e => new Slice(
                e.GetProperty("engine").GetString()!,
                e.GetProperty("project").GetString()!,
                e.GetProperty("filter").GetString()!,
                e.GetProperty("shard").GetString()!,
                e.GetProperty("slug").GetString()!,
                e.TryGetProperty("timeout", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0))
            .ToList();
    }
}
