using System.Net;
using System.Net.Http.Headers;

using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// An address the catalogue has no page for answers with the catalogue's own page,
/// inside its layout, and still as a 404.
/// </summary>
/// <remarks>
/// Without the status-code middleware the response was the server's bare 404, so a
/// mistyped address showed the browser's own error page with no way back into the site.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class NotFoundTests(CatalogueAppFixture app)
{
    [Theory]
    [InlineData("/xxx")]
    [InlineData("/xx/yy")]
    [InlineData("/button/nope")]
    public async Task An_unknown_address_answers_404_with_the_catalogue_page(string path)
    {
        var response = await app.Client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("There is no page at this address", html, StringComparison.Ordinal);
        // The layout, so the sidebar and the search are there to get back with.
        Assert.Contains("class=\"cat-drawer\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_not_found_page_is_in_no_navigation_group() =>
        Assert.DoesNotContain(CataloguePages.All, p => p.Route == CataloguePages.NotFoundRoute);

    [Fact]
    public async Task The_mcp_endpoint_is_never_answered_with_the_html_page()
    {
        // A GET is not a JSON-RPC call, so the endpoint refuses it — and a refusal with no
        // body is exactly what the status-code middleware would otherwise fill with HTML.
        // Headers only: whatever the transport answers, the body is not read.
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/mcp", UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await app.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
