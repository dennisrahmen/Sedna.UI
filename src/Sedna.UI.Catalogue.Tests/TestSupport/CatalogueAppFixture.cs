using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// The running catalogue and a Chromium instance, started once for the whole
/// suite.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WebApplicationFactory{TEntryPoint}"/>'s default server is in-memory
/// and has no socket, so Playwright cannot reach it. <c>UseKestrel(0)</c> binds a
/// free port and <c>StartServer()</c> writes the resolved address into
/// <c>ClientOptions.BaseAddress</c> — which is the whole port-discovery story, and
/// the reason this is not a <c>dotnet run</c> in the background: no process to
/// kill on a failure path, no port to guess, and readiness is the return of a
/// method rather than a poll.
/// </para>
/// <para>
/// It is Kestrel either way, so nothing about the fidelity of what is being tested
/// changes.
/// </para>
/// <para>
/// A collection fixture rather than one instance per class: starting the app and
/// launching a browser costs more than the page work in most of these tests.
/// </para>
/// </remarks>
public sealed class CatalogueAppFixture : IAsyncLifetime
{
    // Shared with the library's own suite. Two suites, one switch.
    private const string OptOutEnvVar = "SEDNA_UI_BROWSER_TESTS";

    private WebApplicationFactory<Program>? _factory;
    private IPlaywright? _playwright;

    /// <summary>The launched browser, or null when none could be started.</summary>
    public IBrowser? Browser { get; private set; }

    /// <summary>Playwright's first line of complaint, or null when the launch worked.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>Where the app is listening. Real HTTP on a real port.</summary>
    public Uri BaseAddress { get; private set; } = new("http://localhost/");

    /// <summary>An <see cref="HttpClient"/> against the running app.</summary>
    public HttpClient Client => _factory!.CreateClient();

    /// <summary>True when the caller has deliberately opted out of the browser tests.</summary>
    public static bool OptedOut => Environment.GetEnvironmentVariable(OptOutEnvVar) == "0";

    /// <summary>True when no browser started, so a browser test would assert nothing.</summary>
    public bool NoBrowser => Unavailable is not null;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _factory.UseKestrel(0);
        _factory.StartServer();
        BaseAddress = _factory.ClientOptions.BaseAddress;

        try
        {
            _playwright = await Playwright.CreateAsync();
            Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
        catch (Exception ex)
        {
            // Almost always "Executable doesn't exist" — the binaries were never
            // downloaded. Recorded rather than thrown so the source scans still run.
            Unavailable = ex.Message.Split('\n')[0];
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
    }

    /// <summary>The absolute URL of a route on the running app.</summary>
    public string Url(string route) => new Uri(BaseAddress, route).ToString();

    /// <summary>
    /// Opens a page and waits for the Blazor circuit, so a test that clicks a C#
    /// handler is not racing the connection.
    /// </summary>
    /// <remarks>
    /// Waits on <c>data-interactive</c>, which the layout renders from
    /// <c>RendererInfo.IsInteractive</c> — Blazor's own signal, rather than a sleep
    /// long enough to usually work. Tests that only read computed CSS do not need
    /// this and should use <see cref="OpenAsync"/>.
    /// </remarks>
    public async Task<IPage> OpenInteractiveAsync(string route)
    {
        var page = await OpenAsync(route);
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    /// <summary>Opens a page and waits for the server-rendered document.</summary>
    public async Task<IPage> OpenAsync(string route)
    {
        var page = await Browser!.NewPageAsync();
        var response = await page.GotoAsync(Url(route), new() { WaitUntil = WaitUntilState.Load });

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"{route} returned {response.Status}.");
        return page;
    }

    /// <summary>
    /// The gate. Called by one test in this suite; the assertion is what stops a
    /// missing browser from being a silent pass.
    /// </summary>
    public void AssertBrowserAvailable()
    {
        if (OptedOut) return;

        Assert.True(Unavailable is null,
            "No browser could be launched, so every browser test would have asserted nothing. Run "
            + "`pwsh src/Sedna.UI.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`, "
            + $"or set {OptOutEnvVar}=0 to run only the source scans. Playwright said: {Unavailable}");
    }
}

/// <summary>
/// Binds every browser test class in this suite to one running app and one
/// browser.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CatalogueAppCollection : ICollectionFixture<CatalogueAppFixture>
{
    public const string Name = "catalogue app";
}
