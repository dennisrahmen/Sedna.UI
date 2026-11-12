using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>/pdf</c> in a real browser: the sample is served as a PDF, the frame takes its height or a
/// page's shape, and where the browser draws no PDF the app's fallback is what the reader sees,
/// centred where the document would have been.
/// </summary>
/// <remarks>
/// Neither test engine draws a PDF inside a page. Headless Chromium reports
/// <c>navigator.pdfViewerEnabled</c> false, shows the object's fallback, and downloads the file an
/// iframe points at. Playwright's WebKit reports it true and lists <c>application/pdf</c>, and still
/// shows the fallback, with an iframe left empty — which is why the page tells an app to write the
/// fallback whatever the flag says. Both are asserted, so an engine that starts drawing PDFs in the
/// page fails here and the page's account of it is revisited.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class PdfPageTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(app.Url("/pdf"), new() { WaitUntil = WaitUntilState.Load });
        return page;
    }

    [Fact]
    public async Task The_sample_is_served_as_a_pdf()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);

        var response = await page.Context.APIRequest.GetAsync(app.Url("/samples/delivery-note-1042.pdf"));
        Assert.Equal(200, response.Status);
        Assert.StartsWith("application/pdf", response.Headers["content-type"], StringComparison.Ordinal);
        var body = await response.BodyAsync();
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(body, 0, 5));
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_fallback_shows_where_the_browser_draws_no_pdf_and_the_frame_keeps_its_size()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);

        var result = await page.EvaluateAsync<string>("""
            () => {
                const out = [];
                out.push('viewer flag ' + navigator.pdfViewerEnabled);
                const frames = [...document.querySelectorAll('.pdf-frame')];
                if (frames.length < 2) out.push('frames ' + frames.length);
                for (const [i, frame] of frames.entries()) {
                    frame.scrollIntoView({ block: 'center' });
                    const f = frame.getBoundingClientRect();
                    const obj = frame.querySelector(':scope > object');
                    const bar = frame.querySelector(':scope > .pdf-frame-bar');
                    const o = obj.getBoundingClientRect();
                    const barHeight = bar ? bar.getBoundingClientRect().height : 0;
                    // The object fills the frame below the bar, inside the frame's own border,
                    // which a --flush frame in a card does not draw.
                    const cs = getComputedStyle(frame);
                    const bx = parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth);
                    const by = parseFloat(cs.borderTopWidth) + parseFloat(cs.borderBottomWidth);
                    if (Math.abs(o.width - (f.width - bx)) > 1) out.push(`#${i} object width ${o.width} of ${f.width}`);
                    if (Math.abs(o.height - (f.height - by - barHeight)) > 1) out.push(`#${i} object height ${o.height} of ${f.height} - ${barHeight}`);
                    if (frame.classList.contains('pdf-frame--page')) {
                        if (Math.abs(f.height / f.width - 297 / 210) > 0.01) out.push(`#${i} page ratio ${f.height / f.width}`);
                    } else {
                        // The default height, or the one the app set with --pdf-height.
                        const set = parseFloat(frame.style.getPropertyValue('--pdf-height'));
                        const expected = Number.isNaN(set) ? Math.min(innerHeight * 0.8, 960) : set;
                        if (Math.abs(f.height - expected) > 1) out.push(`#${i} height ${f.height}, expected ${expected}`);
                    }
                    const fallback = obj.querySelector('.empty-state');
                    const b = fallback.getBoundingClientRect();
                    const shown = b.width > 0 && b.height > 0 && getComputedStyle(fallback).visibility !== 'hidden';
                    if (!shown) out.push(`#${i} fallback hidden`);
                    if (shown) {
                        // Centred where the document would have been, inside the object's box.
                        const dx = (b.left + b.width / 2) - (o.left + o.width / 2);
                        const dy = (b.top + b.height / 2) - (o.top + o.height / 2);
                        if (Math.abs(dx) > 1 || Math.abs(dy) > 1) out.push(`#${i} fallback off centre ${dx},${dy}`);
                        if (b.top < o.top - 0.5 || b.bottom > o.bottom + 0.5) out.push(`#${i} fallback spills ${b.top}-${b.bottom} of ${o.top}-${o.bottom}`);
                        const link = fallback.querySelector('a[download]');
                        if (!link || link.getBoundingClientRect().height === 0) out.push(`#${i} no download link`);
                    }
                }
                return out.join('; ');
            }
            """);

        var parts = result.Split("; ");
        Assert.True(parts.Length == 1, $"{BrowserEngine.Name}: {result}");
        // What each engine says of itself, and draws regardless.
        Assert.Equal(BrowserEngine.IsWebKit ? "viewer flag true" : "viewer flag false", parts[0]);
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
