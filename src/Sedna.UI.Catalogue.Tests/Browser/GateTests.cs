using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The single test that fails when no browser is available, so a browserless
/// machine reports one clear reason instead of a dozen confusing ones.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class GateTests(CatalogueAppFixture app)
{
    [Fact]
    public void A_browser_is_available() => app.AssertBrowserAvailable();

    [Fact]
    public async Task The_app_is_listening()
    {
        // Not gated on the browser: this is the other half of the fixture, and it
        // is worth knowing which one is broken.
        var response = await app.Client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.True(response.IsSuccessStatusCode,
            $"GET /health returned {(int)response.StatusCode}. Railway's healthcheck uses this, "
            + "and a deploy never goes live without it.");
    }
}
