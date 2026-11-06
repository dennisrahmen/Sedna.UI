using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The browser floor is current Chromium and current Safari on iOS. A pull request runs a
/// smoke pass; the release tag runs every test project in every engine, and publishes only
/// when all of them are green.
/// </summary>
/// <remarks>
/// The engines are <see cref="BrowserEngine.All"/> and the test projects are every
/// <c>*.Tests</c> directory under <c>src/</c>. An engine or a test project the release
/// matrix does not name would go untested while the build stayed green, so each is
/// checked here rather than remembered.
/// </remarks>
public class BrowserFloorTests
{
    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(Assets.RepoRoot, ".github", "workflows", name));

    private static IReadOnlyList<string> MatrixList(string yaml, string key)
    {
        var match = Regex.Match(yaml, $@"^\s+{key}:\s*\[(?<items>[^\]]*)\]", RegexOptions.Multiline);
        Assert.True(match.Success, $"release.yml has no `{key}: [...]` matrix entry.");
        return match.Groups["items"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    [Fact]
    public void The_release_tests_every_engine()
    {
        var engines = MatrixList(Workflow("release.yml"), "engine");
        foreach (var engine in BrowserEngine.All)
            Assert.True(engines.Contains(engine), $"release.yml's test matrix does not run {engine}.");
    }

    [Fact]
    public void The_release_tests_every_test_project()
    {
        var projects = MatrixList(Workflow("release.yml"), "project");
        var expected = Directory.GetDirectories(Path.Combine(Assets.RepoRoot, "src"), "*.Tests")
            .Select(d => "src/" + Path.GetFileName(d))
            .ToList();

        Assert.NotEmpty(expected);
        foreach (var project in expected)
            Assert.True(projects.Contains(project), $"release.yml's test matrix does not run {project}.");
    }

    [Fact]
    public void The_release_runs_the_browser_tests_in_the_matrix_engine()
    {
        var yaml = Workflow("release.yml");
        Assert.Matches(@"playwright\.ps1 install --with-deps \$\{\{ matrix\.engine \}\}", yaml);
        Assert.Matches(@"SEDNA_UI_BROWSER_TESTS:\s*'1'", yaml);
        Assert.Matches(@"SEDNA_UI_BROWSER:\s*\$\{\{ matrix\.engine \}\}", yaml);
    }

    [Fact]
    public void Nothing_publishes_before_the_full_suite_is_green()
    {
        var yaml = Workflow("release.yml");
        var release = Regex.Match(yaml, @"^  release:\s*\n\s+needs:\s*\[(?<needs>[^\]]*)\]", RegexOptions.Multiline);

        Assert.True(release.Success, "release.yml's release job declares no `needs:` list.");
        Assert.Contains("test", release.Groups["needs"].Value.Split(',', StringSplitOptions.TrimEntries));
    }
}
