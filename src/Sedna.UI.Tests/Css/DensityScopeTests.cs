using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests.Css;

/// <summary>
/// Density is a scope, as a variant is: <c>data-density</c> on any element remaps the tokens for
/// what is inside it. Every density remaps the same tokens, and <c>comfortable</c> — the default
/// given back to a part of a compact page — holds exactly the values <c>:root</c> declares, so it
/// cannot drift from them.
/// </summary>
public class DensityScopeTests
{
    private static string Part(string name) =>
        Assets.StripComments(File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", name)));

    private static Dictionary<string, string> Block(string css, string density)
    {
        var m = Regex.Match(css, $@":root :where\(\[data-density=""{density}""\]\)[^{{]*\{{(?<body>[^{{}}]*)\}}");
        Assert.True(m.Success, $"No block for data-density=\"{density}\" that reaches any element.");
        return Regex.Matches(m.Groups["body"].Value, @"(?<n>--[a-z0-9-]+)\s*:\s*(?<v>[^;]+);")
            .ToDictionary(x => x.Groups["n"].Value, x => x.Groups["v"].Value.Trim(), StringComparer.Ordinal);
    }

    [Fact]
    public void Every_density_remaps_the_same_tokens_and_comfortable_is_the_default()
    {
        var css = Part("90-density.css");
        var tokens = Part("01-tokens.css");
        var compact = Block(css, "compact");
        var touch = Block(css, "touch");
        var comfortable = Block(css, "comfortable");

        Assert.Equal(compact.Keys.Order(StringComparer.Ordinal), touch.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(compact.Keys.Order(StringComparer.Ordinal), comfortable.Keys.Order(StringComparer.Ordinal));

        foreach (var (name, value) in comfortable)
        {
            var declared = Regex.Match(tokens, $@"(?<![-\w]){Regex.Escape(name)}\s*:\s*(?<v>[^;]+);");
            Assert.True(declared.Success, $"{name} is not declared in 01-tokens.css.");
            Assert.Equal(declared.Groups["v"].Value.Trim(), value);
        }
    }
}
