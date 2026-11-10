using System.Text;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every checkbox the stylesheet draws itself draws the half-ticked state too: a "select all"
/// over a list some of whose rows are ticked.
/// </summary>
/// <remarks>
/// <para>
/// A checkbox with <c>appearance: none</c> has no look the library did not give it, so a ticked
/// box drawn without a half-ticked one shows a mixed "select all" as an empty box — a lie about
/// the rows below it. <c>.form-check</c> was the box that lacked it; the tree's had it.
/// </para>
/// <para>
/// A drawn checkbox is found by how its ticked state is written: <c>:checked</c> on a checkbox
/// input, or <c>[aria-checked="true"]</c> on an element that is one. Each rule that styles the
/// ticked state needs the same rule for the half-ticked one — <c>:indeterminate</c> and
/// <c>[aria-checked="mixed"]</c> for an input, <c>[aria-checked="mixed"]</c> for an ARIA checkbox —
/// or one rule naming both. A switch has no half state, and a listbox's ticked row is
/// <c>aria-selected</c>, which has no third value, so neither is a checkbox here.
/// </para>
/// </remarks>
public class CheckboxMixedTests
{
    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    private const string Mixed = @"(?::indeterminate|\[aria-checked=""mixed""\])";
    private const string InputMixed = @":is(:indeterminate, [aria-checked=""mixed""])";
    private const string AriaTicked = @"[aria-checked=""true""]";
    private const string AriaMixed = @"[aria-checked=""mixed""]";

    /// <summary>Every selector in the sheet outside its media blocks, squashed.</summary>
    private static List<string> Selectors() =>
        Rule.Matches(Tokens.WithoutMediaBlocks(Assets.StripComments(Assets.Css)))
            .SelectMany(m => SplitSelectors(m.Groups["selector"].Value))
            .Select(Assets.Squash)
            .Where(s => !s.StartsWith('@'))
            .ToList();

    /// <summary>The selectors that style a drawn checkbox's ticked state, with the ticked state as written.</summary>
    private static List<(string Selector, string Ticked)> TickedStates(IEnumerable<string> selectors)
    {
        var found = new List<(string, string)>();
        foreach (var s in selectors)
        {
            if (Regex.IsMatch(s, Mixed) || Regex.IsMatch(s, @"\.switch\b")) continue;
            // `:not(:checked)` is the unticked state, which needs no counterpart.
            var ticked = Regex.Replace(s, @":not\([^()]*\)", "");
            if (ticked.Contains(@"[type=""checkbox""]", StringComparison.Ordinal) && ticked.Contains(":checked", StringComparison.Ordinal))
                found.Add((s, ":checked"));
            if (ticked.Contains(AriaTicked, StringComparison.Ordinal))
                found.Add((s, AriaTicked));
        }
        return found;
    }

    /// <summary>The ticked-state selectors with no half-ticked selector in the same place.</summary>
    private static List<string> Uncovered(IReadOnlyCollection<string> selectors)
    {
        var all = selectors.ToHashSet(StringComparer.Ordinal);
        return TickedStates(selectors)
            .Where(t =>
            {
                bool Has(string replacement) => all.Contains(t.Selector.Replace(t.Ticked, replacement, StringComparison.Ordinal));
                return t.Ticked == ":checked"
                    ? !Has(InputMixed) && !(Has(":indeterminate") && Has(AriaMixed))
                    : !Has(AriaMixed);
            })
            .Select(t => t.Selector)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void Every_drawn_checkbox_draws_the_half_ticked_state()
    {
        var selectors = Selectors();
        var ticked = TickedStates(selectors);

        // The discovery finds the boxes known to be drawn, so an empty list is a broken test and
        // not a pass.
        Assert.Contains(ticked, t => t.Selector == @".form-check input[type=""checkbox""]:checked::after");
        Assert.Contains(ticked, t => t.Selector.StartsWith(@".tree [aria-checked=""true""]", StringComparison.Ordinal));

        var missing = Uncovered(selectors);
        Assert.True(missing.Count == 0,
            "These rules draw a checkbox's ticked state and no rule draws its half-ticked state in the same "
            + "place, so a \"select all\" over a partly ticked list shows nothing there. Name the half state "
            + "beside each — :is(:checked, :indeterminate, [aria-checked=\"mixed\"]) where it looks the same, "
            + "or a rule of its own with :is(:indeterminate, [aria-checked=\"mixed\"]) where it draws the dash:\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void A_drawn_checkbox_without_the_half_state_is_found()
    {
        string[] planted =
        [
            @".pick-list input[type=""checkbox""]:checked",
            @".pick-list input[type=""checkbox""]:checked::after",
            @".pick-list input[type=""checkbox""]:is(:indeterminate, [aria-checked=""mixed""])::after",
            @".pick-list:has(input[type=""checkbox""]:not(:checked)) + .pick-more",
            @".pick-row[aria-checked=""true""] > .pick-box",
            @".switch input[type=""checkbox""]:checked",
        ];

        Assert.Equal(
            [@".pick-list input[type=""checkbox""]:checked", @".pick-row[aria-checked=""true""] > .pick-box"],
            Uncovered(planted));
    }

    private static IEnumerable<string> SplitSelectors(string list)
    {
        var depth = 0;
        var current = new StringBuilder();
        foreach (var c in list)
        {
            if (c == '(') depth++;
            if (c == ')') depth--;
            if (c == ',' && depth == 0)
            {
                if (current.ToString().Trim() is { Length: > 0 } s) yield return s;
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        if (current.ToString().Trim() is { Length: > 0 } last) yield return last;
    }
}
