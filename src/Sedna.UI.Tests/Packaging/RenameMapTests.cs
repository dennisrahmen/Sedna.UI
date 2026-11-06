using System.Text.Json;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A release's rename map (docs/renames/&lt;version&gt;.json) is what an app converts by, so it
/// has to be right: every old name is gone from the stylesheet, and every new one is in it.
/// </summary>
public class RenameMapTests
{
    public static IEnumerable<object[]> Maps() =>
        Directory.GetFiles(Path.Combine(Assets.RepoRoot, "docs", "renames"), "*.json").Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Maps))]
    public void Every_old_name_is_gone_and_every_new_one_is_declared(string file)
    {
        var css = Assets.StripComments(Assets.Css);
        bool Declared(string c) => Regex.IsMatch(css, $@"\.{Regex.Escape(c)}(?![\w-])");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Assets.RepoRoot, "docs", "renames", file)));
        Assert.Equal(Path.GetFileNameWithoutExtension(file), doc.RootElement.GetProperty("release").GetString());
        var renames = doc.RootElement.GetProperty("renames").EnumerateArray().ToList();
        Assert.NotEmpty(renames);

        var problems = new List<string>();
        foreach (var r in renames)
        {
            var old = r.GetProperty("old").GetString()!;
            if (Declared(old)) problems.Add($"{old} is still declared, so it is not a rename");
            foreach (var n in r.GetProperty("new").EnumerateArray().Select(e => e.GetString()!))
                if (!Declared(n)) problems.Add($"{old} → {n}, which the stylesheet does not declare");
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// The release notes call the attached map renames-&lt;version&gt;.json. An asset is
    /// named after its file, and the file in the repository is named after the version
    /// alone, so the release has to upload it under the other name.
    /// </summary>
    [Fact]
    public void The_release_attaches_the_map_under_the_name_the_notes_give_it()
    {
        var yaml = File.ReadAllText(Path.Combine(Assets.RepoRoot, ".github", "workflows", "release.yml"));
        Assert.Matches(@"gh release upload [^\n]*""renames-\$VERSION\.json""", yaml);
    }
}
