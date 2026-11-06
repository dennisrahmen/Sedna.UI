using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A browser test spells a platform-dependent key chord through <c>Keys</c>, never as a
/// literal, in both test projects.
/// </summary>
/// <remarks>
/// Tab in WebKit on macOS, and Control as the primary modifier, are what a literal gets
/// wrong: the test passes on the Linux runner and fails on a developer's Mac, or the other
/// way round. Plain Home, End and arrows are allowed — the page under test handles those
/// itself, and they are the same everywhere.
/// </remarks>
public class KeySpellingTests
{
    private static readonly Regex Literal = new(
        @"PressAsync\(\s*""(?<key>(?:[A-Za-z]+\+)*(?:Tab|Control\+[^""]+|Meta\+[^""]+|Shift\+(?:Home|End)))""",
        RegexOptions.Compiled);

    [Fact]
    public void No_test_spells_a_platform_dependent_chord()
    {
        var offenders = new[] { "Sedna.UI.Tests", "Sedna.UI.Catalogue.Tests" }
            .Select(p => Path.Combine(Assets.RepoRoot, "src", p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && Path.GetFileName(f) != "Keys.cs")
            .SelectMany(f => Literal.Matches(File.ReadAllText(f))
                .Select(m => $"{Path.GetRelativePath(Assets.RepoRoot, f)}: \"{m.Groups["key"].Value}\""))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Spell these through TestSupport/Keys (Keys.Tab, Keys.Primary(...), Keys.LineEnd, …):\n"
            + string.Join('\n', offenders));
    }
}
