using System.Text.RegularExpressions;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// The tone system as the shipped stylesheet declares it (69-tone.css): which tones each
/// family takes, and the token each tone puts behind each <c>--tone-*</c> property.
/// </summary>
internal static class Tones
{
    private static readonly Regex Carrier = new(
        @"(?<selectors>\.[a-z]+(?:-[a-z]+)*--[a-z]+(?:,\s*\.[a-z]+(?:-[a-z]+)*--[a-z]+)*)\s*\{(?<body>[^{}]*--tone-fg\s*:[^{}]*)\}", RegexOptions.Compiled);

    /// <summary>tone → (property without <c>--tone-</c> → token it reads, e.g. <c>--warn-bg</c>).</summary>
    public static Dictionary<string, Dictionary<string, string>> Carriers()
    {
        var css = Assets.StripComments(Assets.Css);
        var found = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (Match m in Carrier.Matches(css))
        {
            var tone = Regex.Match(m.Groups["selectors"].Value, @"--([a-z]+)").Groups[1].Value;
            var props = Regex.Matches(m.Groups["body"].Value, @"--tone-(?<p>[a-z-]+)\s*:\s*var\(\s*(?<t>--[a-z0-9-]+)\s*\)")
                .ToDictionary(x => x.Groups["p"].Value, x => x.Groups["t"].Value, StringComparer.Ordinal);
            if (props.Count > 0) found[tone] = props;
        }
        return found;
    }

    /// <summary>family → the tones its carriers name it in, e.g. <c>btn</c> → brand, go, …</summary>
    public static Dictionary<string, SortedSet<string>> Families()
    {
        var css = Assets.StripComments(Assets.Css);
        var families = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (Match m in Carrier.Matches(css))
            foreach (Match s in Regex.Matches(m.Groups["selectors"].Value, @"\.(?<f>[a-z]+(?:-[a-z]+)*)--(?<t>[a-z]+)"))
            {
                if (!families.TryGetValue(s.Groups["f"].Value, out var set))
                    families[s.Groups["f"].Value] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add(s.Groups["t"].Value);
            }
        return families;
    }
}
