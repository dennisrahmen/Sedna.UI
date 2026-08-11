using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Release conventions that only a test can hold: the Releases page is the changelog.
/// </summary>
public class ReleaseConventionTests
{
    [Fact]
    public void There_is_no_changelog_file()
    {
        // Release notes live on the GitHub Releases page, generated from each
        // annotated tag's message — written once at tagging time, alongside the
        // release they describe. A file in the working tree would be a second
        // place to keep in sync, and the one that silently goes stale.
        var stray = new[] { "CHANGELOG.md", "CHANGELOG", "CHANGES.md", "HISTORY.md", "RELEASES.md" }
            .Where(name => File.Exists(Path.Combine(Assets.RepoRoot, name)))
            .ToList();

        Assert.True(stray.Count == 0,
            "This repo deliberately has no changelog file — the Releases page is the changelog, and " +
            "notes come from the annotated tag message. See CLAUDE.md §Releasing. Found: " +
            string.Join(", ", stray));
    }
}
