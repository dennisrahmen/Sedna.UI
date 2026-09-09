using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every class the library puts on a <c>&lt;dialog&gt;</c> answers the UA stylesheet.
/// </summary>
/// <remarks>
/// <para>
/// A <c>&lt;dialog&gt;</c> arrives with more UA styling than any other element the
/// library touches: <c>display: none</c>, <c>margin: auto</c>, <c>border: solid</c>,
/// <c>padding: 1em</c>, <c>width</c>/<c>height: fit-content</c>, both inline insets at
/// 0, and — for <c>:modal</c> — <c>max-width</c>/<c>max-height</c> caps. A class that
/// styles a dialog and leaves one of those unanswered does not fail; it produces a
/// panel that is subtly the wrong shape, and every rule the author wrote parses and
/// applies.
/// </para>
/// <para>
/// Three separate bugs came from exactly that, all in the drawer: a "full-height"
/// panel sized to its own text, an end-anchored panel that jumped to the start edge,
/// and 3px of <c>currentColor</c> border along three viewport edges — which in the dark
/// theme is a white frame.
/// </para>
/// <para>
/// Only the <c>display</c> rule is checked here, because it is the one a source scan
/// can state exactly: the declaration has to be gated on <c>[open]</c>, and that is
/// visible in the selector. Whether the UA's border, padding and sizing have actually
/// been answered is a question about the cascade — a plain <c>.modal</c> rule answers
/// the UA just as well as a <c>dialog.modal</c> one, and <c>.sheet</c> is answered by
/// <c>dialog.drawer</c> because it is never used alone. That is measured on real
/// elements in <see cref="OverlayLayoutTests"/>.
/// </para>
/// </remarks>
public class DialogTests
{
    public static TheoryData<string> DialogClasses()
    {
        var css = Assets.StripComments(Assets.Css);

        // Discovered, not listed: a new dialog class is covered the moment it is
        // written, which is the whole point of a guard like this.
        var found = Regex.Matches(css, @"(?<![\w-])dialog\.(?<name>[a-z][a-z0-9-]*)")
            .Select(m => m.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(found);

        var data = new TheoryData<string>();
        foreach (var name in found) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(DialogClasses))]
    public void A_dialog_class_gates_its_display_on_open(string name)
    {
        // A class that gives a dialog a display without gating it overrides the UA's
        // `display: none` for the CLOSED state too, and the panel sits in the page for
        // the rest of the session. The palette did exactly this: built once on the
        // first Ctrl-K, then visible below the content forever after.
        var css = Assets.StripComments(Assets.Css);

        var ungated = Regex.Matches(css, @"(?<selector>[^{}@;]*dialog\.[^{}@;]*)\{(?<body>[^{}]*)\}")
            .Where(m => m.Groups["selector"].Value.Contains($"dialog.{name}", StringComparison.Ordinal))
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])display\s*:\s*(?!none)"))
            .Select(m => Assets.Squash(m.Groups["selector"].Value))
            .Where(selector => !selector.Contains("[open]", StringComparison.Ordinal))
            .ToList();

        Assert.True(ungated.Count == 0,
            $"These give dialog.{name} a display that also applies while it is closed. "
            + $"Gate it on [open]:{Environment.NewLine}{string.Join(Environment.NewLine, ungated)}");
    }

    [Theory]
    [MemberData(nameof(DialogClasses))]
    public void A_dialog_class_hides_itself_while_closed(string name)
    {
        // The theory above only sees selectors that NAME the element — `dialog.modal`.
        // A BARE class rule is the same bug and was invisible to it: `.modal { display:
        // flex }` applies to a <dialog> just as well, outranks the UA's
        // `dialog:not([open]) { display: none }` by being an author rule, and leaves the
        // panel in the page before its trigger is ever clicked and again after it closes.
        // That shipped, and the guard passed the whole time.
        //
        // So this asks the question from the other side: whatever gave the class a
        // display, is the closed state answered somewhere? One companion rule does it for
        // every form the display can be written in, which is why it is required rather
        // than merely allowed.
        var css = Assets.StripComments(Assets.Css);

        var display = Regex.Matches(css, @"(?<selector>[^{}@;]*)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(
                m.Groups["selector"].Value,
                $@"(?<![\w-])\.{Regex.Escape(name)}(?![\w-])"))
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])display\s*:\s*(?!none)"))
            .Where(m => !m.Groups["selector"].Value.Contains("[open]", StringComparison.Ordinal))
            .Select(m => Assets.Squash(m.Groups["selector"].Value))
            .ToList();

        if (display.Count == 0) return;

        // `dialog.<name>:not([open]) { display: none }`. The element is named on purpose:
        // the same class is also worn by non-dialog markup — `.modal` sits on a
        // `.modal-backdrop` div in the fallback form, and hiding that unconditionally
        // would break it.
        var hidden = Regex.Matches(css, @"(?<selector>[^{}@;]*)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(
                m.Groups["selector"].Value,
                $@"(?<![\w-])dialog\.{Regex.Escape(name)}:not\(\[open\]\)"))
            .Any(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])display\s*:\s*none"));

        Assert.True(hidden,
            $"`.{name}` is given a display by {display.Count} rule(s) that do not mention "
            + $"[open], so it also applies to a CLOSED <dialog> and the panel sits in the "
            + $"page for the rest of the session. Add "
            + $"`dialog.{name}:not([open]) {{ display: none; }}`. The rules:"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, display)}");
    }

}
