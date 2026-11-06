using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every address a shipped script writes into an href passed <c>ui._.safeHref</c> first.
/// </summary>
/// <remarks>
/// The Markdown renderer checked its links' schemes; the palette, the search and a graph
/// record's <c>data-href</c> did not, so a <c>javascript:</c> address handed over in app
/// data was followed. One rule now, and this finds a new place that writes an href
/// without it. A Blob URL the script made itself (an export to download) is its own and
/// needs no check.
/// </remarks>
public class HrefSafetyTests
{
    private static readonly Regex Write = new(
        @"(?:\.href\s*=\s*(?<value>[^;]+);|setAttribute\(\s*'href'\s*,\s*(?<value>[^)]+)\))", RegexOptions.Compiled);

    [Fact]
    public void Every_href_a_script_writes_passed_the_one_address_rule()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Assets.ProjectDir), "*.js", SearchOption.AllDirectories)
                     .Where(f => f.Contains($"{Path.DirectorySeparatorChar}js-parts{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              || f.Contains($"{Path.DirectorySeparatorChar}graph-parts{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              || f.Contains($"{Path.DirectorySeparatorChar}editor-parts{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = Write.Match(lines[i]);
                if (!m.Success) continue;
                var value = m.Groups["value"].Value.Trim();
                var before = string.Join('\n', lines[Math.Max(0, i - 8)..i]) + "\n" + lines[i];
                var checkedHere = before.Contains("safeHref", StringComparison.Ordinal);
                var ownBlob = before.Contains("URL.createObjectURL(", StringComparison.Ordinal);
                var passedIn = Regex.IsMatch(before, @"function\s+go\(\s*" + Regex.Escape(value) + @"\s*\)");
                if (!checkedHere && !ownBlob && !passedIn)
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0, "Writes an href without ui._.safeHref:\n" + string.Join('\n', offenders));
    }
}
