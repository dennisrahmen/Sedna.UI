using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The gate for every browser suite.
/// </summary>
/// <remarks>
/// The browser binaries are not restored with the package, and a browser test that
/// passes without a browser asserts nothing — which is worse than one that fails. Every
/// other browser test returns early when no browser started; this is the one that fails,
/// so a browserless machine reports a single clear reason instead of dozens.
/// </remarks>
public class GateTests : BrowserTestBase
{
    [Fact]
    public void A_browser_is_available() => AssertBrowserAvailable();
}
