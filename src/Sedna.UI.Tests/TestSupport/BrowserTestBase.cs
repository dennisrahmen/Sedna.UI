using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// A Chromium instance and the opt-out gate, shared by every test class that needs a
/// real browser.
/// </summary>
/// <remarks>
/// <para>
/// The browser binaries are not restored with the package, and a test that passes
/// without asserting anything is worse than one that fails — so a missing browser is a
/// <b>failure by default</b>. Install it once:
/// </para>
/// <code>pwsh bin/Debug/net10.0/playwright.ps1 install chromium</code>
/// <para>
/// <c>SEDNA_UI_BROWSER_TESTS=0</c> opts out, for the rare case of running the source scans
/// on a machine that genuinely cannot host a browser. It has to be set deliberately,
/// which is the point: the quiet path is the honest one.
/// </para>
/// <para>
/// One browser per test class, because xUnit creates one instance of the class per test
/// method and <see cref="IAsyncLifetime"/> runs around each of them. Launching Chromium
/// costs far less than the page work, and sharing one across classes would need a
/// collection fixture and give the tests a shared mutable dependency.
/// </para>
/// </remarks>
public abstract class BrowserTestBase : IAsyncLifetime
{
    private const string OptOutEnvVar = "SEDNA_UI_BROWSER_TESTS";

    private IPlaywright? _playwright;

    /// <summary>The launched browser, or null when none could be started.</summary>
    protected IBrowser? Browser { get; private set; }

    /// <summary>Playwright's first line of complaint, or null when the launch worked.</summary>
    protected string? Unavailable { get; private set; }

    /// <summary>True when the caller has deliberately opted out of the browser tests.</summary>
    protected static bool OptedOut =>
        Environment.GetEnvironmentVariable(OptOutEnvVar) == "0";

    public async Task InitializeAsync()
    {
        try
        {
            _playwright = await Playwright.CreateAsync();
            Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
        catch (Exception ex)
        {
            // Almost always "Executable doesn't exist" — the binaries were never
            // downloaded. Recorded rather than thrown so the rest of the suite runs.
            Unavailable = ex.Message.Split('\n')[0];
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
    }

    /// <summary>
    /// True when no browser started. Every browser-dependent test returns early on this,
    /// and <c>A_browser_is_available</c> is the single test that fails — so a browserless
    /// machine reports one clear failure instead of dozens of confusing ones, and
    /// <c>SEDNA_UI_BROWSER_TESTS=0</c> turns even that one into a quiet pass.
    /// </summary>
    protected bool NoBrowser => Unavailable is not null;

    /// <summary>
    /// The gate. Called by one test per browser suite; the assertion is what stops a
    /// missing browser from being a silent pass.
    /// </summary>
    protected void AssertBrowserAvailable()
    {
        if (OptedOut) return;

        Assert.True(Unavailable is null,
            "No browser could be launched, so every browser test would have asserted nothing. Run "
            + "`pwsh src/Sedna.UI.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`, "
            + $"or set {OptOutEnvVar}=0 to run only the source scans. Playwright said: {Unavailable}");
    }
}
