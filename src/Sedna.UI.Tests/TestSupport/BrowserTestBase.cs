using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// A browser instance and the opt-out gate, shared by every test class that needs a
/// real browser.
/// </summary>
/// <remarks>
/// <para>
/// The browser binaries are not restored with the package, and a test that passes
/// without asserting anything is worse than one that fails — so a missing browser is a
/// <b>failure by default</b>. Install it once:
/// </para>
/// <code>pwsh bin/Debug/net10.0/playwright.ps1 install chromium webkit</code>
/// <para>
/// The floor is two engines, so the suite runs once per engine: <c>SEDNA_UI_BROWSER</c> is
/// <c>chromium</c> (the default) or <c>webkit</c>, and CI runs both. Any other value is a
/// failure, so a typo cannot quietly fall back to the default engine.
/// </para>
/// <para>
/// <c>SEDNA_UI_BROWSER_TESTS=0</c> opts out, for the rare case of running the source scans
/// on a machine that genuinely cannot host a browser. It has to be set deliberately,
/// which is the point: the quiet path is the honest one.
/// </para>
/// <para>
/// One browser per test process. xUnit creates one instance of the class per test method
/// and per theory row, and <see cref="IAsyncLifetime"/> runs around each of them, so a
/// browser launched there was launched and torn down once per test. The Playwright driver
/// and the browser are instead started by the first test that asks, under a lock, and
/// shared by every class running in parallel. What a test must not share is state, so each
/// test gets its own <see cref="IBrowserContext"/> from <see cref="NewContextAsync"/> —
/// its own cookies, storage, permissions and pages — which is closed when the test ends.
/// Nothing may call <c>NewContextAsync</c> on the browser itself: a context made there is
/// not closed until the process exits and is the leak this arrangement exists to prevent.
/// </para>
/// <para>
/// A browser that dies mid-run — WebKit can crash — fails the tests that were on it, with
/// Playwright's own closed-target error, and the next test to start gets a fresh one. The
/// browser and the driver are closed when the process exits.
/// </para>
/// </remarks>
public abstract class BrowserTestBase : IAsyncLifetime
{
    private const string OptOutEnvVar = "SEDNA_UI_BROWSER_TESTS";

    private readonly List<IBrowserContext> _contexts = new();
    private readonly List<ICDPSession> _coverage = new();
    private IBrowser? _browser;

    /// <summary>The engine this run drives: <c>chromium</c> or <c>webkit</c>.</summary>
    protected static string Engine => BrowserEngine.Name;

    /// <summary>
    /// Starts V8 coverage collection on a page the test has just created, before it navigates —
    /// a no-op unless <c>SEDNA_UI_JS_COVERAGE_DIR</c> is set. Taken when the test ends, before
    /// its contexts close.
    /// </summary>
    protected async Task TrackCoverageAsync(IBrowserContext context, IPage page)
    {
        if (await JsCoverage.AttachAsync(context, page) is { } session)
            lock (_contexts) _coverage.Add(session);
    }

    /// <summary>
    /// A new context in the shared browser, closed when the test ends. Every context a test
    /// opens comes from here, with whatever options it needs.
    /// </summary>
    protected async Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null)
    {
        var context = await _browser!.NewContextAsync(options);
        lock (_contexts) _contexts.Add(context);
        return context;
    }

    /// <summary>Playwright's first line of complaint, or null when the launch worked.</summary>
    protected string? Unavailable { get; private set; }

    /// <summary>True when the caller has deliberately opted out of the browser tests.</summary>
    protected static bool OptedOut =>
        Environment.GetEnvironmentVariable(OptOutEnvVar) == "0";

    public async Task InitializeAsync()
    {
        // Opted out means no browser at all, not "whichever happens to be installed":
        // the smoke pass in CI and a quick local run both rely on it to skip the browser
        // tests, and they did not skip on a machine that had one.
        if (OptedOut)
        {
            Unavailable = $"{OptOutEnvVar}=0";
            return;
        }

        try
        {
            _browser = await SharedBrowser.GetAsync();
        }
        catch (Exception ex) when (SharedBrowser.IsLaunchFailure(ex))
        {
            // Almost always "Executable doesn't exist" — the binaries were never
            // downloaded. Recorded rather than thrown so the rest of the suite runs.
            Unavailable = ex.Message.Split('\n')[0];
        }
    }

    public async Task DisposeAsync()
    {
        IBrowserContext[] contexts;
        ICDPSession[] sessions;
        lock (_contexts)
        {
            contexts = _contexts.ToArray();
            sessions = _coverage.ToArray();
            _contexts.Clear();
            _coverage.Clear();
        }

        await JsCoverage.DrainAsync(sessions);

        // A context on a browser that has died cannot be closed, and its test has already
        // failed on the closed target; a live browser that will not close one is a defect.
        foreach (var context in contexts)
        {
            try { await context.CloseAsync(); }
            catch when (_browser is { IsConnected: false }) { }
        }
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
            + $"`pwsh src/Sedna.UI.Tests/bin/Debug/net10.0/playwright.ps1 install {Engine}`, "
            + $"or set {OptOutEnvVar}=0 to run only the source scans. Playwright said: {Unavailable}");
    }
}
