using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The tooling an app runs against its own files: the Stylelint config, the class check the
/// package adds to the app's build, the rename script and the agent skill. Each is generated
/// from, or reads, a source in this repository, and each is run here the way an app runs it.
/// </summary>
public class AppToolingTests
{
    private static string StylelintPath => Path.Combine(Assets.RepoRoot, "tooling", "stylelint", "sedna-ui.stylelintrc.json");
    private static string SkillPath => Path.Combine(Assets.RepoRoot, "skills", "sedna-ui", "SKILL.md");
    private static string TargetsPath => Path.Combine(Assets.ProjectDir, "Tooling", "Sedna.UI.targets");
    private static string CodemodPath => Path.Combine(Assets.RepoRoot, "build", "rename-classes.py");
    private static string RenameMaps => Path.Combine(Assets.RepoRoot, "docs", "renames");

    // ── The generated files ──────────────────────────────────────────────────────────

    [Fact]
    public void The_stylelint_config_and_the_skill_match_their_sources()
    {
        var (exit, output) = Run("bash", Assets.RepoRoot, Path.Combine(Assets.RepoRoot, "build", "app-tooling.sh"), "--check");
        Assert.True(exit == 0, "Run build/app-tooling.sh:\n" + output);
    }

    [Fact]
    public void The_stylelint_config_disallows_exactly_the_classes_the_stylesheet_declares()
    {
        var listed = SelectorPatterns().Select(p => Regex.Match(p, @"^/\\\.(.+)\(\?!\[\\w-\]\)/$").Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var declared = Assets.ClassSelectors(Assets.StripComments(Assets.Css));

        Assert.True(declared.SetEquals(listed),
            "Run build/app-tooling.sh. Missing: " + string.Join(", ", declared.Except(listed).Order(StringComparer.Ordinal))
            + ". Not declared: " + string.Join(", ", listed.Except(declared).Order(StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData(".btn", true)]
    [InlineData(".btn:hover", true)]
    [InlineData(".myapp-toolbar .btn--go.btn--solid", true)]
    [InlineData("dialog.modal[open]", true)]
    [InlineData(".myapp-btn", false)]
    [InlineData(".myapp-card", false)]
    [InlineData(".btnx", false)]
    [InlineData("#btn", false)]
    public void A_selector_is_reported_when_it_names_a_class_the_library_owns(string selector, bool reported)
    {
        // Stylelint compiles each "/…/" entry as a JavaScript RegExp; the syntax used is the
        // subset .NET reads the same way.
        var hit = SelectorPatterns().Any(p => Regex.IsMatch(selector, p[1..^1]));
        Assert.Equal(reported, hit);
    }

    [Fact]
    public void A_colour_literal_is_reported_everywhere_but_as_a_token_value_in_brand_css()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(StylelintPath));
        var rules = doc.RootElement.GetProperty("rules");
        Assert.True(rules.GetProperty("color-no-hex")[0].GetBoolean());
        Assert.Equal("never", rules.GetProperty("color-named")[0].GetString());
        var functions = rules.GetProperty("function-disallowed-list")[0].EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("rgb", functions);
        Assert.Contains("hsl", functions);
        Assert.Contains("oklch", functions);

        var brand = doc.RootElement.GetProperty("overrides").EnumerateArray()
            .Single(o => o.GetProperty("files").EnumerateArray().Any(f => f.GetString() == "**/brand.css"))
            .GetProperty("rules");
        Assert.Equal(JsonValueKind.Null, brand.GetProperty("color-no-hex").ValueKind);

        // brand.css redefines tokens the library declares, and no other name.
        var pattern = new Regex(brand.GetProperty("custom-property-pattern")[0].GetString()!);
        foreach (var token in Assets.DeclaredCustomProperties(Assets.StripComments(Assets.Css)))
            Assert.Matches(pattern, token[2..]);
        Assert.DoesNotMatch(pattern, "myapp-panel");
    }

    [Fact]
    public void The_skill_is_the_consuming_app_block_with_front_matter()
    {
        var skill = File.ReadAllText(SkillPath).ReplaceLineEndings("\n");
        var doc = File.ReadAllText(Path.Combine(Assets.RepoRoot, "docs", "CLAUDE.consuming-app.md")).ReplaceLineEndings("\n");

        var front = Regex.Match(skill, @"\A---\nname: (?<name>[a-z0-9-]+)\ndescription: (?<description>[^\n]+)\n---\n");
        Assert.True(front.Success, "SKILL.md opens with front matter holding name and description.");
        Assert.Equal("sedna-ui", front.Groups["name"].Value);
        Assert.InRange(front.Groups["description"].Value.Length, 1, 1024);

        var block = doc[(doc.IndexOf("\n---\n", StringComparison.Ordinal) + "\n---\n".Length)..];
        Assert.True(skill.EndsWith(block, StringComparison.Ordinal),
            "SKILL.md must end with docs/CLAUDE.consuming-app.md after its first rule, unchanged. Run build/app-tooling.sh");
    }

    // ── The class check in the app's build ───────────────────────────────────────────

    [Fact]
    public void The_class_check_warns_with_the_file_the_line_the_old_class_and_its_replacement()
    {
        using var app = new Fixture();
        var maps = app.Write("maps/9.1.0.json", """
            { "release": "9.1.0", "from": "9.0.0", "about": "A fixture.",
              "renames": [ { "old": "panel-old", "new": [ "panel", "panel--raised" ] } ] }
            """);
        var history = app.Write("history.json", """
            { "removed": { "classes": {
                "panel-old":  { "since": "9.0.0", "removed": "9.1.0" },
                "panel-gone": { "since": "9.0.0", "removed": "9.1.0" },
                "panel-new":  { "since": "9.1.0", "removed": null } } } }
            """);
        app.Write("Pages/Orders.razor", """
            <section class="card">
              <div class="panel-old sedna-row">Open orders</div>
              <span class="my-panel-old panel-old-x">Not a library class</span>
              <p class="panel-new">Due today</p>
            </section>
            """);
        app.Write("wwwroot/css/app.css", """
            .myapp-list .panel-gone:hover { gap: var(--space-2); }
            /* panel-gone in a sentence is no selector */
            """);
        app.Write("wwwroot/lib/vendor/vendor.css", ".panel-old { gap: 0; }");
        app.Write("bin/Debug/stale.html", "<div class=\"panel-old\"></div>");

        var (exit, warnings, output) = app.Build(
            $"-p:SednaUiRenameMapDirectory={Path.GetDirectoryName(maps)}{Path.DirectorySeparatorChar}",
            $"-p:SednaUiClassHistory={history}");

        Assert.True(exit == 0, output);
        Assert.Equal(
        [
            "Pages/Orders.razor(2,15) SEDNAUI001: Sedna.UI 9.1.0 renamed the class 'panel-old' to 'panel panel--raised'. Replace the whole class, never a prefix: tooling/rename-classes.py in the package does it.",
            "Pages/Orders.razor(4,13) SEDNAUI002: Sedna.UI removed the class 'panel-new' with no rename, so it styles nothing. The release notes say what to use instead.",
            "wwwroot/css/app.css(1,14) SEDNAUI002: Sedna.UI 9.1.0 removed the class 'panel-gone' with no rename, so it styles nothing. The release notes say what to use instead.",
        ], warnings);
    }

    [Fact]
    public void The_class_check_names_every_rename_in_the_committed_maps()
    {
        var renames = Directory.GetFiles(RenameMaps, "*.json").SelectMany(file =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var release = doc.RootElement.GetProperty("release").GetString()!;
            return doc.RootElement.GetProperty("renames").EnumerateArray()
                .Select(r => (Old: r.GetProperty("old").GetString()!, New: string.Join(' ', r.GetProperty("new").EnumerateArray().Select(n => n.GetString())), Release: release))
                .ToList();
        }).ToList();
        Assert.NotEmpty(renames);

        using var app = new Fixture();
        app.Write("Pages/Every.razor", string.Join('\n', renames.Select(r => $"<div class=\"{r.Old}\"></div>")));
        var (exit, warnings, output) = app.Build($"-p:SednaUiRenameMapDirectory={RenameMaps}{Path.DirectorySeparatorChar}", "-p:SednaUiClassHistory=none");

        Assert.True(exit == 0, output);
        Assert.Equal(
            renames.Select((r, i) => $"Pages/Every.razor({i + 1},13) SEDNAUI001: Sedna.UI {r.Release} renamed the class '{r.Old}' to '{r.New}'. Replace the whole class, never a prefix: tooling/rename-classes.py in the package does it."),
            warnings);
    }

    [Fact]
    public void The_class_check_is_off_when_the_app_turns_it_off()
    {
        using var app = new Fixture();
        app.Write("Pages/Every.razor", "<button class=\"btn btn-go\">Send</button>");
        var (exit, warnings, output) = app.Build($"-p:SednaUiRenameMapDirectory={RenameMaps}{Path.DirectorySeparatorChar}", "-p:SednaUiClassCheck=false");

        Assert.True(exit == 0, output);
        Assert.Empty(warnings);
    }

    // ── The rename script ────────────────────────────────────────────────────────────

    [Fact]
    public void The_rename_script_prints_a_diff_and_writes_nothing_on_a_dry_run()
    {
        using var app = CodemodFixture();
        var before = File.ReadAllText(app.PathOf("Pages/Orders.razor"));

        var (exit, output) = Run("python3", app.Root, "-I", CodemodPath, "--maps", app.PathOf("maps"), "--from", "9.0.0", "--dry-run", ".");

        Assert.True(exit == 0, output);
        Assert.Contains("-<div class=\"panel-old\">Open</div>", output, StringComparison.Ordinal);
        Assert.Contains("+<div class=\"panel panel--raised\">Open</div>", output, StringComparison.Ordinal);
        Assert.Contains("would change", output, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(app.PathOf("Pages/Orders.razor")));
    }

    [Fact]
    public void The_rename_script_replaces_whole_classes_through_every_release_in_the_range()
    {
        using var app = CodemodFixture();

        var (exit, output) = Run("python3", app.Root, "-I", CodemodPath, "--maps", app.PathOf("maps"), "--from", "9.0.0", ".");

        Assert.True(exit == 0, output);
        Assert.Equal("""
            <div class="panel panel--raised">Open</div>
            <div class="panel panel--raised">Raised already</div>
            <span class="my-panel-old panel-old-x" id="panel-old-note">Not a library class</span>
            <i class="badge-new"></i>
            <style>.panel.panel--raised > .chip { gap: 0; }</style>
            """, File.ReadAllText(app.PathOf("Pages/Orders.razor")));
        Assert.Equal("""
            .myapp-list .panel.panel--raised:hover { gap: var(--space-2); }
            /* panel-old in a sentence is no selector */
            """, File.ReadAllText(app.PathOf("wwwroot/css/app.css")));
        Assert.Contains(".panel-old", File.ReadAllText(app.PathOf("wwwroot/lib/vendor/vendor.css")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_rename_script_stops_at_the_version_it_is_given()
    {
        using var app = CodemodFixture();

        var (exit, output) = Run("python3", app.Root, "-I", CodemodPath, "--maps", app.PathOf("maps"), "--from", "9.0.0", "--to", "9.1.0", "Pages");
        Assert.True(exit == 0, output);
        Assert.Contains("<i class=\"badge-mid\"></i>", File.ReadAllText(app.PathOf("Pages/Orders.razor")), StringComparison.Ordinal);

        (exit, output) = Run("python3", app.Root, "-I", CodemodPath, "--maps", app.PathOf("maps"), "--from", "9.2.0", "Pages");
        Assert.True(exit == 0, output);
        Assert.Contains("Nothing to do", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rename_script_reads_the_committed_maps_by_default()
    {
        using var app = new Fixture();
        app.Write("Pages/Send.razor", "<button class=\"btn btn-go\">Send</button>");

        var (exit, output) = Run("python3", app.Root, "-I", CodemodPath, "--from", "0.27.0", "--to", "0.28.0", ".");

        Assert.True(exit == 0, output);
        Assert.Equal("<button class=\"btn btn--go btn--solid\">Send</button>", File.ReadAllText(app.PathOf("Pages/Send.razor")));
    }

    private static Fixture CodemodFixture()
    {
        var app = new Fixture();
        app.Write("maps/9.1.0.json", """
            { "release": "9.1.0", "from": "9.0.0", "about": "A fixture.",
              "renames": [ { "old": "panel-old", "new": [ "panel", "panel--raised" ] },
                           { "old": "chip-old", "new": [ "chip" ] },
                           { "old": "badge-old", "new": [ "badge-mid" ] } ] }
            """);
        app.Write("maps/9.2.0.json", """
            { "release": "9.2.0", "from": "9.1.0", "about": "A fixture.",
              "renames": [ { "old": "badge-mid", "new": [ "badge-new" ] } ] }
            """);
        app.Write("Pages/Orders.razor", """
            <div class="panel-old">Open</div>
            <div class="panel-old panel--raised">Raised already</div>
            <span class="my-panel-old panel-old-x" id="panel-old-note">Not a library class</span>
            <i class="badge-old"></i>
            <style>.panel-old > .chip-old { gap: 0; }</style>
            """);
        app.Write("wwwroot/css/app.css", """
            .myapp-list .panel-old:hover { gap: var(--space-2); }
            /* panel-old in a sentence is no selector */
            """);
        app.Write("wwwroot/lib/vendor/vendor.css", "/* vendor */ .panel-old { gap: 0; }");
        return app;
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────

    private static List<string> SelectorPatterns()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(StylelintPath));
        return doc.RootElement.GetProperty("rules").GetProperty("selector-disallowed-list")[0]
            .EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static (int Exit, string Output) Run(string fileName, string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        // The test host's own MSBuild variables would point a child build at the wrong toolset.
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>A throwaway app directory: its files, and a build that imports the class check.</summary>
    private sealed class Fixture : IDisposable
    {
        private static readonly Regex Warning = new(
            @"^(?<file>.+?)\((?<line>\d+),(?<column>\d+),\d+,\d+\): Sedna\.UI warning (?<code>SEDNAUI\d{3}): (?<message>.*?) \[",
            RegexOptions.Multiline);

        public string Root { get; } = Directory.CreateTempSubdirectory("sedna-ui-tooling-").FullName;

        public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        public string Write(string relative, string content)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Builds a bare project that imports the targets the package ships as build/Sedna.UI.targets.</summary>
        public (int Exit, List<string> Warnings, string Output) Build(params string[] properties)
        {
            Write("App.proj", $"""
                <Project>
                  <Import Project="{TargetsPath}" />
                  <Target Name="Build" />
                </Project>
                """);
            var (exit, output) = Run(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", Root,
                ["build", "App.proj", "-nologo", "-nodeReuse:false", "-clp:NoSummary", .. properties]);

            var warnings = Warning.Matches(output)
                .Select(m => $"{Path.GetRelativePath(Root, m.Groups["file"].Value).Replace('\\', '/')}({m.Groups["line"].Value},{m.Groups["column"].Value}) {m.Groups["code"].Value}: {m.Groups["message"].Value}")
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return (exit, warnings, output);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
