using Microsoft.Playwright;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// Runs script in a page for its effect, and throws its result away inside the page.
/// </summary>
/// <remarks>
/// <para>
/// An untyped <c>page.EvaluateAsync(...)</c> still sends back whatever the script returned.
/// A one-line arrow function returns its last call, and in the graph that is often the
/// cytoscape core or a collection — an object graph with cycles that WebKit's Playwright
/// serialiser walks without end, so the test hangs with the page idle. Chromium gives up on
/// it, which is how it went unnoticed until the suite ran in both engines.
/// </para>
/// <para>
/// So a script run for its effect goes through here, and <c>EvaluationTests</c> fails on an
/// untyped <c>EvaluateAsync</c> anywhere else. The wrapper is string concatenation, not
/// <c>eval</c>, so it works under a content security policy: a function is called with the
/// argument (or none), an expression is evaluated, and a promise either returns is awaited.
/// </para>
/// </remarks>
internal static class Js
{
    public static Task Run(IPage page, string script) =>
        page.EvaluateAsync($"async () => {{ const v = ({script}); if (typeof v === 'function') await v(); else await v; }}");

    /// <summary>
    /// Finishes every running transition and finite animation on the page, then waits a frame —
    /// for reading a style a state change animates towards. The animation clock, not a
    /// wait: under load a frame can come later than any wait that keeps the suite fast.
    /// </summary>
    public static Task Settle(IPage page) =>
        page.EvaluateAsync("async () => { document.getAnimations().forEach(a => { if (a.effect?.getComputedTiming().endTime !== Infinity) a.finish(); }); await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))); }");

    /// <summary>
    /// The next frames, by the page's own clock of frames rather than by time: every event
    /// handler, observer, promise and frame callback a change set off has run once they have
    /// passed, however slow the machine.
    /// </summary>
    public static Task Frames(IPage page, int count = 2) =>
        page.EvaluateAsync("n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); })", count);

    /// <summary>
    /// Lets the page's own time pass — <paramref name="ms"/> of it, by its clock — and then two
    /// frames: a timer the page set for less than that has run by then, however slow the
    /// machine or the commands that reach it.
    /// </summary>
    public static Task Elapse(IPage page, int ms) =>
        page.EvaluateAsync("""
            ms => new Promise(r => {
                const until = performance.now() + ms;
                const step = () => performance.now() < until ? requestAnimationFrame(step)
                    : requestAnimationFrame(() => requestAnimationFrame(r));
                step();
            })
            """, ms);

    public static Task Run(IPage page, string script, object? arg) =>
        page.EvaluateAsync($"async (a) => {{ const v = ({script}); if (typeof v === 'function') await v(a); else await v; }}", arg);
}
