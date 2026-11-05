using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A test that uses a tool Playwright offers for Chromium only says so through the named
/// capability in <c>BrowserEngine</c>, in both test projects.
/// </summary>
/// <remarks>
/// Without the gate the test fails in WebKit for a reason that is not the library's; with a
/// bare early return it passes there while asserting nothing, and nobody can see that it
/// did. The capability names the gap, and this keeps every use of the tool behind one.
/// </remarks>
public class EngineGapTests
{
    private static readonly (string Tool, Regex Use, string Gate)[] Gaps =
    [
        ("a CDP session", new(@"NewCDPSessionAsync"), "BrowserEngine.HasRawTouch"),
        ("forced-colours emulation", new(@"ForcedColors\.Active"), "BrowserEngine.EmulatesForcedColors"),
        ("reading the clipboard", new(@"clipboard\.readText"), "BrowserEngine.ReadsClipboard"),
        ("a routed request's cookie", new(@"TryGetValue\(""cookie"""), "BrowserEngine.ShowsRoutedCookies"),
    ];

    private static readonly Regex TestStart = new(@"\[(?:Fact|Theory)\]", RegexOptions.Compiled);

    [Fact]
    public void Every_chromium_only_tool_is_behind_its_capability()
    {
        var offenders = new List<string>();

        foreach (var file in new[] { "Sedna.UI.Tests", "Sedna.UI.Catalogue.Tests" }
                     .Select(p => Path.Combine(Assets.RepoRoot, "src", p))
                     .Where(Directory.Exists)
                     .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !f.Contains($"{Path.DirectorySeparatorChar}TestSupport{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && Path.GetFileName(f) != nameof(EngineGapTests) + ".cs"))
        {
            var source = File.ReadAllText(file);
            var starts = TestStart.Matches(source).Select(m => m.Index).Append(source.Length).ToList();

            for (var i = 0; i < starts.Count - 1; i++)
            {
                var test = source[starts[i]..starts[i + 1]];
                var name = Regex.Match(test, @"Task\s+(\w+)\(").Groups[1].Value;

                foreach (var (tool, use, gate) in Gaps)
                    if (use.IsMatch(test) && !test.Contains(gate, StringComparison.Ordinal))
                        offenders.Add($"{Path.GetFileName(file)} {name}: uses {tool} without {gate}");
            }
        }

        Assert.True(offenders.Count == 0, string.Join('\n', offenders));
    }
}
