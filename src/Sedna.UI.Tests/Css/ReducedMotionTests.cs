using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every transition and animation in the library has an off switch under
/// <c>prefers-reduced-motion</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>95-reduced-motion.css</c> opens by saying that scattered per-part media queries
/// are how a new transition ends up with no counterpart, "and nobody notices, because
/// nobody testing the feature has the setting on". That was true of the block itself:
/// nine selectors had drifted in without one — the tab strip, the segmented control,
/// the dropzone, both disclosure carets, the code-block copy button, the skip link and
/// all three drawer surfaces. This is the guard that comment was describing.
/// </para>
/// <para>
/// Matching is by selector text, which is deliberately strict. A near-miss
/// (<c>.modal</c> against <c>dialog.modal</c>) reads as a pass to a human and is
/// exactly the drift worth failing on, since the two do not have to keep the same
/// specificity relationship as the sheet is edited.
/// </para>
/// </remarks>
public class ReducedMotionTests
{
    private const string Part = "95-reduced-motion.css";

    /// <summary>
    /// Animations that carry meaning while they run and are switched off by naming
    /// something narrower than the selector that declared them.
    /// </summary>
    /// <remarks>
    /// Each entry is a selector that declares motion, mapped to the selector in the
    /// reduced-motion block that covers it. The indirection is real rather than an
    /// allowlist: an indeterminate progress bar is filled instead of blanked, so its
    /// off switch has to be the compound selector that also sets the width.
    /// </remarks>
    private static readonly Dictionary<string, string> CoveredBy = new(StringComparer.Ordinal)
    {
        [".progress--indeterminate .progress-bar"] = ".progress--indeterminate .progress-bar",
    };

    [Fact]
    public void Every_transition_and_animation_can_be_switched_off()
    {
        var offSwitches = Selectors(Assets.StripComments(ReadPart(Part)));

        var missing = new List<string>();

        foreach (var part in Directory
                     .EnumerateFiles(Path.Combine(Assets.ProjectDir, "css-parts"), "*.css")
                     .Where(p => Path.GetFileName(p) != Part)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var css = Assets.StripComments(File.ReadAllText(part));

            foreach (var (selector, body) in Assets.TopLevelRules(css))
            {
                if (!DeclaresMotion(body)) continue;

                foreach (var one in selector.Split(',').Select(Normalise).Where(s => s.Length > 0))
                {
                    var wanted = CoveredBy.GetValueOrDefault(one, one);
                    if (!offSwitches.Contains(wanted))
                        missing.Add($"{Path.GetFileName(part)} → {one}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            $"{missing.Count} selectors declare a transition or an animation with no counterpart in "
            + $"{Part}. Add one in the same edit, or the motion stays on for a reader who asked for "
            + $"none:{Environment.NewLine}{string.Join(Environment.NewLine, missing.Distinct())}");
    }

    [Fact]
    public void The_reduced_motion_block_switches_something_off()
    {
        // The vacuity guard. A selector parser that quietly stopped matching would
        // make the test above pass by finding nothing on either side.
        Assert.True(Selectors(Assets.StripComments(ReadPart(Part))).Count >= 20);
    }

    [Fact]
    public void Nothing_in_the_reduced_motion_block_is_unnecessary()
    {
        // The other direction. A line left behind after its rule was deleted is not
        // harmful, but it is a claim that something needs switching off — and the next
        // reader trusts the list to be the inventory it says it is.
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in Directory.EnumerateFiles(
                     Path.Combine(Assets.ProjectDir, "css-parts"), "*.css")
                     .Where(p => Path.GetFileName(p) != Part))
        {
            foreach (var (selector, body) in
                     Assets.TopLevelRules(Assets.StripComments(File.ReadAllText(part))))
            {
                if (!DeclaresMotion(body)) continue;
                foreach (var one in selector.Split(',').Select(Normalise))
                    declared.Add(CoveredBy.GetValueOrDefault(one, one));
            }
        }

        var stale = Selectors(Assets.StripComments(ReadPart(Part)))
            .Except(declared, StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            $"These switch off motion nothing declares any more: {string.Join(", ", stale)}");
    }

    private static bool DeclaresMotion(string body) =>
        Regex.IsMatch(body, @"(?<![\w-])(transition|animation)(-[a-z]+)?\s*:\s*(?!none)",
            RegexOptions.IgnoreCase);

    /// <summary>
    /// Every selector inside the reduced-motion media block, split and normalised.
    /// </summary>
    private static HashSet<string> Selectors(string css) =>
        Assets.MediaBlocks(css)
            .Where(m => m.Condition.Contains("prefers-reduced-motion", StringComparison.Ordinal))
            .SelectMany(m => Assets.TopLevelRules(m.Body))
            .SelectMany(r => r.Selector.Split(','))
            .Select(Normalise)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    private static string Normalise(string selector) => Assets.Squash(selector);

    private static string ReadPart(string name) =>
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", name));
}
