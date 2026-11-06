using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Script run for its effect goes through <c>Js.Run</c>, which drops the result inside the
/// page, in both test projects.
/// </summary>
/// <remarks>
/// An untyped <c>EvaluateAsync</c> sends its result back whether the test wants it or not,
/// and a graph test's one-line script often returns the cytoscape core: WebKit's Playwright
/// serialiser never finishes with it, and the test hangs with the page idle. A typed
/// <c>EvaluateAsync&lt;T&gt;</c> is fine — it asks for a value on purpose.
/// </remarks>
public class EvaluationTests
{
    private static readonly Regex Untyped = new(@"\.EvaluateAsync\(", RegexOptions.Compiled);

    [Fact]
    public void No_test_evaluates_without_saying_what_it_wants_back()
    {
        var offenders = new[] { "Sedna.UI.Tests", "Sedna.UI.Catalogue.Tests" }
            .Select(p => Path.Combine(Assets.RepoRoot, "src", p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && Path.GetFileName(f) != "Js.cs"
                        && Path.GetFileName(f) != nameof(EvaluationTests) + ".cs")
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (line, i))
                .Where(x => Untyped.IsMatch(x.line))
                .Select(x => $"{Path.GetRelativePath(Assets.RepoRoot, f)}:{x.i + 1}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Run these through TestSupport/Js.Run, or ask for a typed result with EvaluateAsync<T>:\n"
            + string.Join('\n', offenders));
    }
}
