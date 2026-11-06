using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every workflow that builds the solution generates the class history first, from a checkout
/// with its tags. The history is not committed and both projects embed it, so a workflow that
/// skips the step fails to build — which is how the code scanning job broke when it moved out
/// of the repository.
/// </summary>
public class WorkflowBuildTests
{
    [Fact]
    public void Every_workflow_that_builds_generates_the_class_history_first()
    {
        var dir = Path.Combine(Assets.RepoRoot, ".github", "workflows");
        var problems = new List<string>();
        var checkedAny = false;
        foreach (var file in Directory.GetFiles(dir, "*.yml"))
        {
            var text = File.ReadAllText(file);
            var build = Regex.Match(text, @"dotnet (?:build|pack|test)(?![^\n]*--no-build)");
            if (!build.Success) continue;
            checkedAny = true;
            var history = text.IndexOf("build/class-history.sh", StringComparison.Ordinal);
            if (history < 0 || history > build.Index)
                problems.Add($"{Path.GetFileName(file)} builds before it generates the class history");
            if (!text.Contains("fetch-depth: 0", StringComparison.Ordinal))
                problems.Add($"{Path.GetFileName(file)} checks out without the tags the history is dated by");
        }

        Assert.True(checkedAny, "No workflow builds the solution, so this measures nothing.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
