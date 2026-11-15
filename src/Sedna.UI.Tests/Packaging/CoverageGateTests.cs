using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>build/coverage-gate.py</c>, run the way CI runs it, over small synthetic Cobertura files
/// and a throwaway git repository: the changed-lines gate, the whole-code floor, the merge of
/// several reports, the harness that measured nothing, and the path forms a report can use.
/// No browser and no real coverage run is involved.
/// </summary>
public class CoverageGateTests : IDisposable
{
    private static string Gate => Path.Combine(Assets.RepoRoot, "build", "coverage-gate.py");
    private static string PolicyPath => Path.Combine(Assets.RepoRoot, "build", "coverage-policy.txt");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sedna-cov-" + Guid.NewGuid().ToString("N")[..10]);

    public CoverageGateTests()
    {
        Directory.CreateDirectory(_dir);
        Git("init", "-q", "-b", "main");
        Write("src/Lib/A.cs", string.Join('\n',
            "// header",             // 1
            "class A",              // 2
            "{",                    // 3
            "    int X() { return 1; }",   // 4
            "    int Y() { return 2; }",   // 5
            "    int Z() { return 3; }",   // 6
            "}", ""));              // 7
        Write("src/Lib/B.cs", "class B\n{\n    int W() { return 4; }\n}\n");   // line 3
        Write("src/Other/C.cs", "class C\n{\n    int V() { return 5; }\n}\n");  // out of scope
        Write("build/coverage-policy.txt", PolicyText());
        Commit("base");
        Git("checkout", "-q", "-b", "feature");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A temporary directory that will not go is left to the system's own cleanup.
        }
    }

    private static string PolicyText(double changed = 80, double floor = 50, string extra = "") =>
        $"changed_lines_min = {changed}\nwhole_floor = {floor}\nscope = src/Lib/ .cs\n{extra}";

    // ── The changed-lines gate ───────────────────────────────────────────────────────

    [Fact]
    public void A_change_whose_executable_lines_are_covered_passes()
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"), (5, "    int Y() { return 20; }"));
        var report = Report(("src/Lib/A.cs", [(4, 3), (5, 1), (6, 0)]), ("src/Lib/B.cs", [(3, 1)]));

        var (exit, output) = Check(report);

        Assert.True(exit == 0, output);
        Assert.Contains("Changed lines: 2 executable, 2 covered", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_change_below_the_threshold_fails_and_lists_each_uncovered_changed_line()
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"), (5, "    int Y() { return 20; }"), (6, "    int Z() { return 30; }"));
        var report = Report(("src/Lib/A.cs", [(4, 3), (5, 0), (6, 0)]));

        var (exit, output) = Check(report);

        Assert.Equal(1, exit);
        Assert.Contains("1 of 3 changed executable lines are covered", output, StringComparison.Ordinal);
        Assert.Contains("src/Lib/A.cs:5", output, StringComparison.Ordinal);
        Assert.Contains("src/Lib/A.cs:6", output, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Lib/A.cs:4", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_lines_the_change_touched_are_judged()
    {
        // Line 6 is uncovered and was not changed: the remainder report shows it, the gate does not.
        ChangeLines("A.cs", (4, "    int X() { return 10; }"));
        var report = Report(("src/Lib/A.cs", [(4, 1), (5, 0), (6, 0)]));

        var (exit, output) = Check(report, "--floor", "0");

        Assert.True(exit == 0, output);
        Assert.Contains("Changed lines: 1 executable, 1 covered", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_change_with_no_executable_line_in_scope_passes_and_says_so()
    {
        ChangeLines("A.cs", (1, "// a new header comment"), (3, "{ "));    // not executable lines of the report
        ChangeLines("../Other/C.cs", (3, "    int V() { return 50; }"));  // executable, out of scope
        var report = Report(("src/Lib/A.cs", [(4, 0), (5, 0)]), ("src/Lib/B.cs", [(3, 0)]));

        var (exit, output) = Check(report, "--floor", "0");

        Assert.True(exit == 0, output);
        Assert.Contains("no executable line in scope changed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_added_file_counts_every_executable_line_of_it()
    {
        Write("src/Lib/D.cs", "class D\n{\n    int Q() { return 1; }\n    int R() { return 2; }\n}\n");
        Commit("add D");
        var report = Report(("src/Lib/D.cs", [(3, 1), (4, 0)]), ("src/Lib/A.cs", [(4, 1)]));

        var (exit, output) = Check(report, "--changed-min", "100");

        Assert.Equal(1, exit);
        Assert.Contains("src/Lib/D.cs:4", output, StringComparison.Ordinal);
    }

    // ── Merging ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_line_is_covered_when_any_report_covers_it()
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"), (5, "    int Y() { return 20; }"));
        var first = Report("one.xml", ("src/Lib/A.cs", [(4, 1), (5, 0)]));
        var second = Report("two.xml", ("src/Lib/A.cs", [(4, 0), (5, 2)]));

        var alone = Check(first);
        var merged = Check(first, second);

        Assert.Equal(1, alone.Exit);
        Assert.True(merged.Exit == 0, merged.Output);
        Assert.Contains("Changed lines: 2 executable, 2 covered", merged.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_directory_of_reports_is_merged_like_a_list_of_them()
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"), (5, "    int Y() { return 20; }"));
        var reports = Path.Combine(_dir, "reports");
        Directory.CreateDirectory(reports);
        Report("reports/one.xml", ("src/Lib/A.cs", [(4, 1), (5, 0)]));
        Report("reports/two.xml", ("src/Lib/A.cs", [(4, 0), (5, 2)]));

        var (exit, output) = Check(reports);

        Assert.True(exit == 0, output);
    }

    // ── The whole-code floor ─────────────────────────────────────────────────────────

    [Fact]
    public void Total_coverage_below_the_floor_fails_even_when_the_change_is_covered()
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"));
        var report = Report(("src/Lib/A.cs", [(4, 1), (5, 0), (6, 0)]), ("src/Lib/B.cs", [(3, 0)]));

        var (exit, output) = Check(report, "--floor", "50");

        Assert.Equal(1, exit);
        Assert.Contains("1 of 4 executable lines covered", output, StringComparison.Ordinal);
        Assert.Contains("below the floor", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_pull_request_the_floor_alone_decides()
    {
        var report = Report(("src/Lib/A.cs", [(4, 1), (5, 1)]));

        var (exit, output) = Check(report, "--skip-changed");

        Assert.True(exit == 0, output);
        Assert.Contains("not checked", output, StringComparison.Ordinal);
    }

    // ── A harness that measured nothing is an error ──────────────────────────────────

    [Fact]
    public void No_report_at_all_is_an_error_and_not_a_pass()
    {
        var empty = Path.Combine(_dir, "empty");
        Directory.CreateDirectory(empty);

        var (exit, output) = Check(empty);

        Assert.Equal(2, exit);
        Assert.Contains("nothing was measured", output, StringComparison.Ordinal);
        Assert.Equal(2, Check(Path.Combine(_dir, "missing.xml")).Exit);
    }

    [Fact]
    public void A_report_with_no_executable_line_in_scope_is_an_error()
    {
        var report = Report(("src/Other/C.cs", [(3, 1)]));

        var (exit, output) = Check(report);

        Assert.Equal(2, exit);
        Assert.Contains("measured no file in scope", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_report_whose_paths_match_no_file_of_the_repository_is_an_error()
    {
        var report = Report(("/somewhere/else/Unrelated.cs", [(1, 1), (2, 1)]));

        var (exit, output) = Check(report);

        Assert.Equal(2, exit);
        Assert.Contains("matches no file of this repository", output, StringComparison.Ordinal);
        Assert.Contains("Unrelated.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public void One_empty_report_among_good_ones_is_still_an_error()
    {
        var good = Report("good.xml", ("src/Lib/A.cs", [(4, 1)]));
        var bad = Report("bad.xml", ("src/Other/C.cs", [(3, 1)]));

        Assert.Equal(2, Check(good, bad).Exit);
    }

    [Fact]
    public void A_file_that_is_not_a_report_is_an_error()
    {
        Write("report.xml", "this is not xml");

        Assert.Equal(2, Check(Path.Combine(_dir, "report.xml")).Exit);
    }

    // ── Paths ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("src/Lib/A.cs", null)]                                  // relative to the repository root
    [InlineData("/Users/someone/Source/Sedna.UI/src/Lib/A.cs", null)]  // another machine, absolute
    [InlineData("/__w/Sedna.UI/Sedna.UI/src/Lib/A.cs", null)]          // a container's checkout
    [InlineData("/_/src/Lib/A.cs", null)]                               // a deterministic build's root
    [InlineData("C:\\a\\Sedna.UI\\src\\Lib\\A.cs", null)]               // Windows separators
    [InlineData("src/Lib/A.cs", "/build/agent/work")]                   // relative, with a <source>
    [InlineData("Lib/A.cs", "/build/agent/work/src")]                   // relative to a source below the root
    public void Every_path_form_a_report_uses_resolves_to_the_repository_file(string filename, string? source)
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"));
        var report = Report("report.xml", source, (filename, [(4, 0)]));

        var (exit, output) = Check(report, "--changed-min", "100");

        Assert.Equal(1, exit);
        Assert.Contains("src/Lib/A.cs:4", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_absolute_path_of_this_machines_checkout_resolves_too()
    {
        ChangeLines("A.cs", (4, "    int X() { return 10; }"));
        var report = Report((Path.Combine(_dir, "src", "Lib", "A.cs"), [(4, 1)]));

        var (exit, output) = Check(report);

        Assert.True(exit == 0, output);
        Assert.Contains("Changed lines: 1 executable, 1 covered", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_of_a_look_alike_directory_is_not_the_scope()
    {
        // src/LibExtra/ shares a prefix with src/Lib/ and is not in scope.
        Write("src/LibExtra/E.cs", "class E\n{\n    int P() { return 1; }\n}\n");
        Commit("lookalike");
        var report = Report(("src/LibExtra/E.cs", [(3, 0)]), ("src/Lib/A.cs", [(4, 1)]));

        var (exit, output) = Check(report);

        Assert.True(exit == 0, output);
        Assert.Contains("no executable line in scope changed", output, StringComparison.Ordinal);
    }

    // ── What is excluded ─────────────────────────────────────────────────────────────

    [Fact]
    public void An_excluded_file_is_neither_measured_nor_judged_and_the_report_names_its_reason()
    {
        SetPolicy("exclude = src/Lib/B.cs | wiring with nothing to assert");
        ChangeLines("B.cs", (3, "    int W() { return 40; }"));
        var report = Report(("src/Lib/A.cs", [(4, 1)]), ("src/Lib/B.cs", [(3, 0)]));
        var markdown = Path.Combine(_dir, "out.md");

        var (exit, output) = Check(report, "--markdown", markdown);

        Assert.True(exit == 0, output);
        Assert.Contains("no executable line in scope changed", output, StringComparison.Ordinal);
        var text = File.ReadAllText(markdown);
        Assert.Contains("`src/Lib/B.cs`: wiring with nothing to assert", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exclusion_without_a_reason_is_an_error()
    {
        SetPolicy("exclude = src/Lib/B.cs");
        var report = Report(("src/Lib/A.cs", [(4, 1)]));

        var (exit, output) = Check(report);

        Assert.Equal(2, exit);
        Assert.Contains("reason is required", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exclusion_that_matches_no_file_is_an_error()
    {
        SetPolicy("exclude = src/Lib/Gone.cs | it was deleted");
        var report = Report(("src/Lib/A.cs", [(4, 1)]));

        var (exit, output) = Check(report);

        Assert.Equal(2, exit);
        Assert.Contains("matches no file", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_in_code_exclusion_needs_a_justification()
    {
        Write("src/Lib/F.cs", "[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\nclass F { }\n");
        Commit("unjustified");
        var report = Report(("src/Lib/A.cs", [(4, 1)]));

        var (exit, output) = Check(report);

        Assert.Equal(2, exit);
        Assert.Contains("src/Lib/F.cs:1", output, StringComparison.Ordinal);

        Write("src/Lib/F.cs", "[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = \"a type the host constructs\")]\nclass F { }\n");
        Commit("justified");
        Assert.Equal(0, Check(report).Exit);
    }

    // ── The remainder report ─────────────────────────────────────────────────────────

    [Fact]
    public void The_markdown_report_lists_the_uncovered_lines_per_file_most_first()
    {
        var report = Report(
            ("src/Lib/A.cs", [(4, 0), (5, 0), (6, 1), (7, 0)]),
            ("src/Lib/B.cs", [(3, 0)]));
        var markdown = Path.Combine(_dir, "out.md");

        var (_, output) = Check(report, "--skip-changed", "--floor", "0", "--markdown", markdown);

        var text = File.ReadAllText(markdown);
        Assert.Contains("| `src/Lib/A.cs` | 4 | 3 | 25.0% | 4-5, 7 |", text, StringComparison.Ordinal);
        Assert.Contains("| `src/Lib/B.cs` | 1 | 1 | 0.0% | 3 |", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("src/Lib/A.cs", StringComparison.Ordinal) < text.IndexOf("src/Lib/B.cs", StringComparison.Ordinal), output);
        Assert.Contains("Whole code: 1 of 5 executable lines covered (20.0%", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_of_the_scope_that_no_report_mentions_is_listed()
    {
        var report = Report(("src/Lib/A.cs", [(4, 1)]));
        var markdown = Path.Combine(_dir, "out.md");

        Check(report, "--skip-changed", "--markdown", markdown);

        var text = File.ReadAllText(markdown);
        Assert.Contains("In scope, in no report", text, StringComparison.Ordinal);
        Assert.Contains("`src/Lib/B.cs`", text, StringComparison.Ordinal);
    }

    // ── The script's coverage ────────────────────────────────────────────────────────

    private static string JsConverter => Path.Combine(Assets.RepoRoot, "build", "coverage-js.py");

    private string ServedScriptCoverage(string name, int[][] covered, string? hash = null)
    {
        var served = Assets.JsPath;
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            scripts = new Dictionary<string, object>
            {
                ["/js/Sedna.UI.js"] = new
                {
                    length = File.ReadAllText(served).Length,
                    sha256 = hash ?? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(served))).ToLowerInvariant(),
                    covered,
                },
            },
        });
        Write(name, json);
        return Path.Combine(_dir, name);
    }

    [Fact]
    public void The_served_script_maps_back_to_its_parts_exactly()
    {
        // The converter fails unless build/strip-comments.py's output, replayed with line numbers,
        // equals the served file and every mapped line equals its line in a part.
        var length = File.ReadAllText(Assets.JsPath).Length;
        var input = ServedScriptCoverage("js-a.json", [[0, length]]);
        var output = Path.Combine(_dir, "js.xml");

        var (exit, text) = Run("python3", Assets.RepoRoot, "-I", JsConverter, "--repo", Assets.RepoRoot, "--out", output, input);

        Assert.True(exit == 0, text);
        Assert.Contains("JavaScript:", text, StringComparison.Ordinal);
        var xml = File.ReadAllText(output);
        Assert.Contains("filename=\"src/Sedna.UI/js-parts/00-core.js\"", xml, StringComparison.Ordinal);
        var core = System.Text.RegularExpressions.Regex.Match(xml, "filename=\"src/Sedna.UI/js-parts/00-core.js\">.*?</class>").Value;
        Assert.Contains("hits=\"1\"", core, StringComparison.Ordinal);
        Assert.DoesNotContain("hits=\"0\"", core, StringComparison.Ordinal);
    }

    [Fact]
    public void Offsets_that_ran_in_either_of_two_processes_are_covered()
    {
        var length = File.ReadAllText(Assets.JsPath).Length;
        var half = length / 2;
        var first = ServedScriptCoverage("js-first.json", [[0, half]]);
        var second = ServedScriptCoverage("js-second.json", [[half, length]]);

        string Covered(params string[] inputs)
        {
            var output = Path.Combine(_dir, "js-merge.xml");
            var args = new List<string> { "-I", JsConverter, "--repo", Assets.RepoRoot, "--out", output };
            args.AddRange(inputs);
            var (exit, text) = Run("python3", Assets.RepoRoot, args.ToArray());
            Assert.True(exit == 0, text);
            return System.Text.RegularExpressions.Regex.Match(text, @"JavaScript: (\d+) of").Groups[1].Value;
        }

        var one = int.Parse(Covered(first), System.Globalization.CultureInfo.InvariantCulture);
        var two = int.Parse(Covered(second), System.Globalization.CultureInfo.InvariantCulture);
        var both = int.Parse(Covered(first, second), System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(one > 0 && two > 0 && both >= Math.Max(one, two) && both <= one + two, $"{one} {two} {both}");
    }

    [Fact]
    public void Coverage_measured_against_another_build_of_the_script_is_an_error()
    {
        var input = ServedScriptCoverage("js-stale.json", [[0, 10]], hash: new string('0', 64));

        var (exit, text) = Run("python3", Assets.RepoRoot, "-I", JsConverter, "--repo", Assets.RepoRoot,
            "--out", Path.Combine(_dir, "js.xml"), input);

        Assert.Equal(2, exit);
        Assert.Contains("changed since it was measured", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_script_coverage_at_all_is_an_error()
    {
        var empty = Path.Combine(_dir, "js-empty");
        Directory.CreateDirectory(empty);

        var (exit, text) = Run("python3", Assets.RepoRoot, "-I", JsConverter, "--repo", Assets.RepoRoot,
            "--out", Path.Combine(_dir, "js.xml"), empty);

        Assert.Equal(2, exit);
        Assert.Contains("nothing was measured", text, StringComparison.Ordinal);
    }

    // ── The committed pieces ─────────────────────────────────────────────────────────

    [Fact]
    public void The_committed_policy_states_the_rule_and_neither_number_is_a_target()
    {
        var text = File.ReadAllText(PolicyPath);

        Assert.Matches(@"(?m)^changed_lines_min\s*=\s*\d+(\.\d+)?\s*$", text);
        Assert.Matches(@"(?m)^whole_floor\s*=\s*\d+(\.\d+)?\s*$", text);
        Assert.Contains("ever raised because the suite exceeds it", text, StringComparison.Ordinal);
        foreach (var line in text.Split('\n').Where(l => l.StartsWith("exclude", StringComparison.Ordinal)))
            Assert.Matches(@"^exclude\s*=\s*\S+\s*\|\s*\S.+$", line);
    }

    [Fact]
    public void The_shell_driver_parses_and_the_gate_prints_its_usage()
    {
        var (syntax, output) = Run("bash", Assets.RepoRoot, "-n", Path.Combine(Assets.RepoRoot, "build", "coverage.sh"));
        Assert.True(syntax == 0, output);

        var (exit, help) = Run("python3", Assets.RepoRoot, "-I", Gate, "--help");
        Assert.Equal(0, exit);
        Assert.Contains("--skip-changed", help, StringComparison.Ordinal);
    }

    // ── Harness ──────────────────────────────────────────────────────────────────────

    /// <summary>Runs the gate in the temporary repository: reports and options in any order.</summary>
    private (int Exit, string Output) Check(params string[] arguments)
    {
        var args = new List<string> { "-I", Gate, "--repo", _dir, "--base", "main" };
        args.AddRange(arguments);
        return Run("python3", _dir, args.ToArray());
    }

    /// <summary>Rewrites lines of a file in <c>src/Lib</c> and commits, which is the pull request.</summary>
    private void ChangeLines(string file, params (int Line, string Text)[] edits)
    {
        var path = Path.GetFullPath(Path.Combine(_dir, "src", "Lib", file));
        var lines = File.ReadAllText(path).Split('\n');
        foreach (var (line, text) in edits) lines[line - 1] = text;
        File.WriteAllText(path, string.Join('\n', lines));
        Commit("change " + file);
    }

    private string Report(params (string File, (int Line, int Hits)[] Lines)[] files) => Report("report.xml", null, files);

    private string Report(string name, params (string File, (int Line, int Hits)[] Lines)[] files) => Report(name, null, files);

    private string Report(string name, string? source, params (string File, (int Line, int Hits)[] Lines)[] files)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<coverage><sources>");
        if (source is not null) xml.Append("<source>").Append(source).Append("</source>");
        xml.Append("</sources><packages><package name=\"p\"><classes>");
        foreach (var (file, lines) in files)
        {
            // The same line appears under the method and under the class, as the collector writes it.
            var rows = string.Concat(lines.Select(l => $"<line number=\"{l.Line}\" hits=\"{l.Hits}\" branch=\"False\"/>"));
            xml.Append($"<class name=\"c\" filename=\"{file}\"><methods><method name=\"m\"><lines>{rows}</lines></method></methods><lines>{rows}</lines></class>");
        }
        xml.Append("</classes></package></packages></coverage>");
        Write(name, xml.ToString());
        return Path.Combine(_dir, name);
    }

    private void SetPolicy(string extra) =>
        File.WriteAllText(Path.Combine(_dir, "build", "coverage-policy.txt"), PolicyText(extra: extra));

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void Commit(string message)
    {
        Git("add", "-A");
        Git("-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", message);
    }

    private void Git(params string[] arguments) => Assert.Equal(0, Run("git", _dir, arguments).Exit);

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
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
