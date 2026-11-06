namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// Key chords whose spelling depends on the platform the browser runs on.
/// </summary>
/// <remarks>
/// <para>
/// The floor is current Chromium and current Safari, and the suite runs on Linux in CI and
/// on macOS for a developer. Two things differ there, and a test that spells the key out
/// passes on one and fails on the other:
/// </para>
/// <list type="bullet">
/// <item><b>Moving the caret in text.</b> macOS has no Home/End/Control+End for a line or a
/// document; it uses Command with an arrow. A shortcut the page binds as "primary" (Quill's
/// <c>shortKey</c>, the palette) is Command there as well.</item>
/// <item><b>Tab in WebKit on macOS</b> reaches only text fields unless the reader turned on
/// keyboard navigation; Option+Tab reaches every focusable element, which is what a test
/// of focus order means.</item>
/// </list>
/// <para>
/// A key the page itself handles — Home and End in a tablist, an arrow in a graph — is the
/// same everywhere and is not routed through here. <c>KeySpellingTests</c> fails on a test
/// that spells one of the chords above directly.
/// </para>
/// </remarks>
internal static class Keys
{
    private static bool Mac => OperatingSystem.IsMacOS();

    /// <summary>Moves focus to the next focusable element, as a keyboard user would.</summary>
    public static string Tab => Mac && BrowserEngine.IsWebKit ? "Alt+Tab" : "Tab";

    /// <summary>Moves focus to the previous focusable element.</summary>
    public static string ShiftTab => Mac && BrowserEngine.IsWebKit ? "Alt+Shift+Tab" : "Shift+Tab";

    /// <summary>The platform's primary modifier with <paramref name="key"/>: Command on macOS, Control elsewhere.</summary>
    public static string Primary(string key) => "ControlOrMeta+" + key;

    /// <summary>Selects everything in the focused text.</summary>
    public static string SelectAll => Primary("A");

    /// <summary>Puts the caret at the end of the current line.</summary>
    public static string LineEnd => Mac ? "Meta+ArrowRight" : "End";

    /// <summary>Extends the selection to the start of the current line.</summary>
    public static string SelectToLineStart => Mac ? "Shift+Meta+ArrowLeft" : "Shift+Home";

    /// <summary>Puts the caret at the end of the document.</summary>
    public static string DocumentEnd => Mac ? "Meta+ArrowDown" : "Control+End";
}
