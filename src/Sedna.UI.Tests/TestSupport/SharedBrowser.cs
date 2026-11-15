using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// The one Playwright driver and the one browser of a test process, started by the first
/// test that asks for it and handed to every test after it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetAsync"/> is safe to call from tests running in parallel: a lock lets one
/// caller start the browser while the others wait for it. A browser whose connection has
/// dropped is replaced, so one crash costs the tests that were on it and no others.
/// </para>
/// <para>
/// A first launch that fails is remembered and rethrown to every later caller, which is how
/// a machine with no browser reports one reason to every test instead of retrying a launch
/// that cannot work. A launch that fails after a browser had already started throws to the
/// test that asked and is tried again by the next, because the cause is then the run and
/// not the machine.
/// </para>
/// <para>
/// The browser and the driver are closed when the process exits, with a time limit, so a
/// driver that does not answer cannot keep the test host alive.
/// </para>
/// </remarks>
internal static class SharedBrowser
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan CloseWithin = TimeSpan.FromSeconds(10);

    private static IPlaywright? _playwright;
    private static IBrowser? _browser;
    private static Exception? _firstLaunchFailure;
    private static bool _everLaunched;
    private static bool _exitHooked;

    /// <summary>How many browsers this process has launched; more than one means one died.</summary>
    public static int Launches { get; private set; }

    public static async Task<IBrowser> GetAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true } live) return live;
            if (_firstLaunchFailure is not null) throw new LaunchFailed(_firstLaunchFailure);

            HookExit();
            try
            {
                _browser = await LaunchAsync();
            }
            catch (PlaywrightException first)
            {
                // The driver may be what died with the browser: the next launch starts its own.
                DisposeDriver();
                if (!HasLaunched())
                {
                    _firstLaunchFailure = first;
                    throw new LaunchFailed(first);
                }

                try
                {
                    _browser = await LaunchAsync();
                }
                catch (PlaywrightException second)
                {
                    DisposeDriver();
                    throw new LaunchFailed(second);
                }
            }

            _everLaunched = true;
            Launches++;
            return _browser;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// True for the failure of getting a browser at all — the ones a test records as "no
    /// browser" — and false for anything a test should see as it is.
    /// </summary>
    public static bool IsLaunchFailure(Exception ex) => ex is LaunchFailed { FirstLaunch: true };

    /// <summary>Whether a browser has ever started in this process.</summary>
    private static bool HasLaunched() => _everLaunched;

    private static async Task<IBrowser> LaunchAsync()
    {
        // A driver that is gone with its browser is replaced along with it.
        _playwright ??= await Playwright.CreateAsync();
        return await BrowserEngine.Of(_playwright).LaunchAsync(new() { Headless = true });
    }

    private static void DisposeDriver()
    {
        try
        {
            _playwright?.Dispose();
        }
        catch (PlaywrightException)
        {
            // It is being replaced because it was already unusable.
        }
        _playwright = null;
        _browser = null;
    }

    private static void HookExit()
    {
        if (_exitHooked) return;
        _exitHooked = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
    }

    private static void Shutdown()
    {
        var browser = _browser;
        var playwright = _playwright;
        _browser = null;
        _playwright = null;

        try
        {
            if (browser is { IsConnected: true })
                Task.Run(() => browser.CloseAsync()).Wait(CloseWithin);
        }
        catch (AggregateException)
        {
            // The process is ending; the driver takes its browsers with it.
        }

        try
        {
            playwright?.Dispose();
        }
        catch (PlaywrightException)
        {
            // The process is ending, and a driver that will not close ends with it.
        }
    }

    /// <summary>
    /// Getting a browser failed. <see cref="FirstLaunch"/> is true when no browser has ever
    /// started in this process.
    /// </summary>
    internal sealed class LaunchFailed : Exception
    {
        public LaunchFailed(Exception inner) : base(inner.Message, inner) =>
            FirstLaunch = !_everLaunched;

        public bool FirstLaunch { get; }
    }
}
