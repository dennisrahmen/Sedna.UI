using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The shipped assets are delivered by <c>MapStaticAssets</c>, which serves the
/// precompressed variants the publish already produced and gives each asset a
/// freshness lifetime.
/// </summary>
/// <remarks>
/// <para>
/// <c>UseStaticFiles</c> can do neither, and the cost was measured rather than assumed:
/// the 466KB stylesheet went out raw, for the proxy in front to re-gzip on the fly to
/// 125KB, where the <c>.br</c> sitting unread beside it in the same directory is 97KB.
/// Across the four compressible assets a cold visit carried about 48KB it did not need
/// to. No <c>Cache-Control</c> was set at all, only an ETag, which leaves the lifetime
/// to a browser's own heuristic.
/// </para>
/// <para>
/// <b>This is a source guard, and it has to be.</b> The compressed variants are written
/// at <i>publish</i> time, not at build time, so the app a <c>WebApplicationFactory</c>
/// starts has no <c>.br</c> to serve however it is configured — an HTTP assertion here
/// would fail on a correct build and pass on nothing. What can be pinned is the
/// decision, and the decision is one word: reverting it breaks no test, serves every
/// byte correctly, and simply makes the site heavier.
/// </para>
/// <para>
/// <c>Cache-Control: no-cache</c> on the two large assets is the right answer and not a
/// missed opportunity. Their URLs are not fingerprinted — deliberately, because the host
/// page is the block <c>docs/getting-started.md</c> tells everyone to paste, and a
/// fingerprinted URL would make the documented page differ from the pasteable one — so a
/// deploy changes the bytes behind an unchanged URL. <c>no-cache</c> is what keeps that
/// deploy visible: the response is still stored, still revalidated by ETag, and still
/// answered with a bodyless 304. A long <c>max-age</c> here would serve last week's
/// stylesheet.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class StaticAssetDeliveryTests(CatalogueAppFixture app)
{
    private static string Program =>
        File.ReadAllText(Path.Combine(CatalogueAssets.AppDir, "Program.cs"));

    [Fact]
    public void The_app_maps_static_assets() =>
        Assert.Contains("app.MapStaticAssets()", Program, StringComparison.Ordinal);

    [Fact]
    public void The_app_does_not_fall_back_to_UseStaticFiles() =>
        Assert.DoesNotContain("UseStaticFiles()", Program, StringComparison.Ordinal);

    /// <summary>
    /// A caller that asks for no encoding gets the file itself. This is what keeps
    /// <see cref="HostPageTests"/>' byte-for-byte comparison meaningful — it would
    /// otherwise be comparing the repo's stylesheet against a brotli stream.
    /// </summary>
    [Fact]
    public async Task An_asset_is_served_uncompressed_when_the_caller_asks_for_nothing()
    {
        using var response = await app.Client.GetAsync("_content/Sedna.UI/css/Sedna.UI.css");
        response.EnsureSuccessStatusCode();

        Assert.Empty(response.Content.Headers.ContentEncoding);
    }
}
