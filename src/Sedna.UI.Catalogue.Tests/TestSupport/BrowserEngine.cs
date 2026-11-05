using Microsoft.Playwright;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// The engine a browser test drives, chosen by <c>SEDNA_UI_BROWSER</c>.
/// </summary>
/// <remarks>
/// The browser floor is current Chromium and current Safari on iOS, so every browser test
/// runs once per engine. The variable names the engine for this run; unset means
/// <c>chromium</c>. An unknown value throws rather than falling back, so a misspelt CI step
/// cannot run the Chromium suite twice and report WebKit as covered.
/// </remarks>
internal static class BrowserEngine
{
    public const string EnvVar = "SEDNA_UI_BROWSER";

    /// <summary>Every engine the floor covers, in the order CI runs them.</summary>
    public static readonly string[] All = ["chromium", "webkit"];

    public static string Name
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrEmpty(value)) return "chromium";
            return All.Contains(value, StringComparer.Ordinal)
                ? value
                : throw new InvalidOperationException(
                    $"{EnvVar}={value} names no engine the floor covers. Use one of: {string.Join(", ", All)}.");
        }
    }

    public static bool IsWebKit => Name == "webkit";

    // ── What Playwright can drive in one engine and not the other ─────────────
    // Every test that cannot run in an engine says why through one of these, so the
    // list of engine gaps is this block and nothing else. None of them is a gap in
    // the library: each is a tool Playwright offers for Chromium only.

    /// <summary>Raw touch input (<c>Input.dispatchTouchEvent</c>) needs a CDP session,
    /// which Playwright offers for Chromium only. WebKit runs the same script.</summary>
    public static bool HasRawTouch => !IsWebKit;

    /// <summary><c>forced-colors</c> emulation is Chromium's; Safari has no forced-colours
    /// mode for a page to meet.</summary>
    public static bool EmulatesForcedColors => !IsWebKit;

    /// <summary>Reading the system clipboard back without a prompt needs the
    /// <c>clipboard-read</c> permission, which Playwright's WebKit does not know.</summary>
    public static bool ReadsClipboard => !IsWebKit;

    /// <summary>A routed request's headers include the cookies the page sent. In WebKit
    /// the network process adds them after Playwright has seen the request.</summary>
    public static bool ShowsRoutedCookies => !IsWebKit;

    public static IBrowserType Of(IPlaywright playwright) =>
        Name == "webkit" ? playwright.Webkit : playwright.Chromium;
}
