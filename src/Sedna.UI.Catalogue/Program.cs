using System.Threading.RateLimiting;
using Sedna.UI;
using Sedna.UI.Catalogue.Components;
using Sedna.UI.Catalogue.Health;
using Sedna.UI.Catalogue.Mcp;
using Sedna.UI.Catalogue.Navigation;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Forest, Cobalt and Graphite exist so the Branding page's live switcher has more
// than one palette to move between — proof the architecture generates a theme from
// one anchor colour rather than hand-tuning a second stylesheet. Cobalt moves the
// brand, Graphite moves the BASE, and Forest moves both — the base being the half
// that used to be impossible to generate and is why every app looked faintly blue.
// SednaBrandStyle
// in App.razor's <head> is what actually emits their [data-theme="…"] blocks; every
// other page keeps rendering Sedna's own palette, since nothing outside that
// page's switch ever sets the attribute.
builder.Services.AddSednaUi(o =>
{
    o.Themes = [SednaTheme.Sedna, SednaTheme.Forest, SednaTheme.Cobalt, SednaTheme.Graphite];
    o.Default = "sedna";
});

// Scoped: one circuit is one browser, and the toggles are per-browser state.

// Built once from the embedded examples, the embedded pages and the stylesheet the
// app serves — so the MCP server and the site cannot describe different bytes.
builder.Services.AddSingleton<CatalogueIndex>();
builder.Services.AddSingleton<VersionEnvelope>();

// The topbar search's index, derived from the same two sources. A singleton
// because it is the same list for everyone: the circuit only pushes it.
builder.Services.AddSingleton<CatalogueSearch>();

builder.Services
    .AddMcpServer(options => options.ServerInstructions = McpInstructions.Text)
    // Stateless: no per-session state, so a flood of `initialize` calls cannot grow
    // the heap. That is an abuse-control property, not only a scaling one.
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<CatalogueTools>()
    .WithResources<CatalogueResources>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // The real ceiling, and the actual security control: not partitioned, so there
    // is nothing to spoof your way around.
    options.AddConcurrencyLimiter("mcp-concurrency", concurrency =>
    {
        concurrency.PermitLimit = 16;
        concurrency.QueueLimit = 8;
    });

    // Fairness between callers. Behind a proxy whose address range we do not
    // control this is spoofable, so it is a fairness measure and never the control.
    // A token bucket rather than a fixed window because legitimate agent traffic is
    // bursty — six get_example calls, then nothing for a minute — and a fixed
    // window punishes exactly that.
    options.AddPolicy("mcp-caller", context =>
        RateLimitPartition.GetTokenBucketLimiter(CallerKey(context),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 60,
                TokensPerPeriod = 30,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "rate_limited", retryAfterSeconds = 60 }, token);
    };
});

// Named, and applied to /mcp only. Not AddDefaultPolicy, which would also cover the
// Blazor app. The endpoint is public and unauthenticated, so there is nothing for a
// cross-origin caller to escalate to — and browser-based MCP clients need this.
builder.Services.AddCors(options => options.AddPolicy("mcp", policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// MCP JSON-RPC requests are a few hundred bytes. This closes the "POST a gigabyte
// at it" hole.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 256 * 1024);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
}

// No UseHttpsRedirection. Railway terminates TLS and always sends
// X-Forwarded-Proto: https, so the app sees plain HTTP — with redirection on and
// forwarded headers off, every request becomes an infinite redirect. The service
// variable ASPNETCORE_FORWARDEDHEADERS_ENABLED=true handles the headers; it also
// clears KnownProxies, which is why the rate limiter above does not rest on the
// remote address.
// Before the endpoint middleware, or an endpoint carrying CORS metadata throws at
// request time rather than at startup — the 500 reads as a broken tool, not a
// missing middleware.
app.UseCors();
app.UseRateLimiter();
app.UseAntiforgery();

// Railway's healthcheck. Must return 200 or the deploy never goes live, which is
// what makes it the right place to assert the host page's own assets are being
// served: a publish that drops one then never replaces a working deployment. The
// endpoint used to answer 200 unconditionally, and a container shipped with no
// _framework/blazor.web.js — every page 200, healthcheck green, nothing on the site
// interactive.
app.MapGet("/health", (IWebHostEnvironment environment) =>
{
    var missing = HostPageAssets.Missing(environment.WebRootFileProvider);

    return missing.Count == 0
        ? Results.Ok("healthy")
        : Results.Json(new { status = "unhealthy", missing },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

// DisableAntiforgery is belt and braces: UseAntiforgery only validates endpoints
// carrying antiforgery metadata, so /mcp should be unaffected — but if POSTs here
// ever start returning bare 400s, this is the cause.
// Both limiters, chained by applying both. Deliberately NOT options.GlobalLimiter:
// that would also count Blazor's SignalR upgrades and every static asset, so one
// person browsing the site would trip a limit sized for MCP calls.
app.MapMcp("/mcp")
    .RequireRateLimiting("mcp-concurrency")
    .RequireRateLimiting("mcp-caller")
    .RequireCors("mcp")
    .DisableAntiforgery();

// The static catalogue's URLs, kept alive because published links outlive a site's
// structure: nuget.org, the README and the docs all point at /catalogue/*.html.
// Driven from the registry, so a new page cannot forget its redirect.
foreach (var page in CataloguePages.All)
{
    var route = page.Route;
    // 301, not 302: these paths are being retired rather than moved again, so
    // spending them permanently is correct.
    app.MapGet($"/catalogue/{page.LegacyFile}", () => Results.Redirect(route, permanent: true));
}

app.MapFallback("/catalogue/{**rest}", () => Results.Redirect("/", permanent: true));

// Routes that existed before a page was split. The site is public and linked from the
// README, nuget.org and the docs, so an old address redirects instead of 404ing.
foreach (var (from, to) in CataloguePages.Moved)
{
    var target = to;
    app.MapGet(from, () => Results.Redirect(target, permanent: true));
}

// MapStaticAssets, not UseStaticFiles, and the difference is measured rather than
// stylistic. The publish already writes a `.br` and a `.gz` beside every compressible
// asset — 466KB of stylesheet becomes 40KB of brotli — and UseStaticFiles cannot serve
// one, so every visitor downloaded the raw bytes and the proxy in front re-gzipped
// them on the fly to a worse ratio. It also sets no `Cache-Control` at all, only an
// ETag, which is a freshness lifetime of zero: every full page load revalidated all
// thirteen requests before anything could render.
//
// This serves the precompressed variant the build already made and gives each asset a
// real lifetime, from the same manifest the SDK emits. It is an endpoint rather than
// middleware, which is why it sits here and not where UseStaticFiles used to.
app.MapStaticAssets();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

// Railway sets X-Real-IP at its edge. Failing that, the RIGHTMOST X-Forwarded-For
// entry is the one the edge appended and therefore the only one a caller cannot
// write themselves.
static string CallerKey(HttpContext context)
{
    var real = context.Request.Headers["X-Real-IP"].ToString();
    if (!string.IsNullOrEmpty(real)) return real;

    var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrEmpty(forwarded)) return forwarded.Split(',')[^1].Trim();

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>
/// Named so <c>WebApplicationFactory&lt;Program&gt;</c> in the test project can
/// find the entry point. Top-level statements otherwise generate an internal
/// class the test assembly cannot name.
/// </summary>
public partial class Program;
