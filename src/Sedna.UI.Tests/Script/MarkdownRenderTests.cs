using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.md.render</c> against a matrix of Markdown and the HTML it must write, and the
/// properties that make its output safe to inject.
/// </summary>
/// <remarks>
/// <para>
/// The matrix is <c>markdown-render-cases.json</c>: one source and the exact HTML for each
/// construct the renderer reads, and for each way a source can try to become markup. It runs
/// in the page — the renderer is the shipped script — so both engines read the same cases.
/// A case that fails names itself and shows what the renderer wrote.
/// </para>
/// <para>
/// The preview is a security boundary. <see cref="Nothing_the_source_says_becomes_markup_outside_the_fixed_set"/>
/// parses what the renderer writes for every case and for a list of hostile sources, and fails on
/// any element or attribute outside the set the renderer writes itself, and on any address with a
/// scheme the library does not keep.
/// </para>
/// </remarks>
public class MarkdownRenderTests : ScriptTestBase
{
    private const string Fixture = """<div id="out"></div>""";

    private sealed record Case(string Name, string Source, string Html);

    private sealed class Mismatch
    {
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public string Want { get; set; } = "";
        public string Got { get; set; } = "";
    }

    private static Case[] Cases()
    {
        var path = Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Tests", "Script", "markdown-render-cases.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateArray()
            .Select(e => new Case(e.GetProperty("name").GetString()!, e.GetProperty("source").GetString()!, e.GetProperty("html").GetString()!))
            .ToArray();
    }

    [Fact]
    public async Task Every_case_in_the_matrix_renders_as_written()
    {
        if (NoBrowser) return;
        var cases = Cases();
        Assert.True(cases.Length > 150);
        var page = await Open(Fixture);

        var wrong = await page.EvaluateAsync<Mismatch[]>("""
            cases => cases.map(c => ({ name: c.name, source: c.source, want: c.html, got: sednaUi.md.render(c.source) }))
                          .filter(c => c.got !== c.want)
            """, cases.Select(c => new { name = c.Name, source = c.Source, html = c.Html }).ToArray());

        Assert.True(wrong.Length == 0,
            $"{wrong.Length} of {cases.Length} cases render differently:\n" + string.Join("\n",
                wrong.Select(w => $"  [{w.Name}] {JsonSerializer.Serialize(w.Source)}\n    want {w.Want}\n    got  {w.Got}")));
    }

    /// <summary>Sources that try to become markup, beyond the ones in the matrix.</summary>
    private static readonly string[] Hostile =
    [
        "<script>boom()</script>", "<img src=x onerror=boom()>", "<svg onload=boom()>", "<a href=\"javascript:boom()\">x</a>",
        "<iframe srcdoc=\"<script>boom()</script>\"></iframe>", "<body onload=boom()>", "<math><mi xlink:href=\"javascript:boom()\">x",
        "[x](javascript:boom())", "[x](JaVaScRiPt:boom())", "[x](  javascript:boom())", "[x](\tjavascript:boom())",
        "[x](jav&#x09;ascript:boom())", "[x](&#106;avascript:boom())", "[x](javascript&#58;boom())", "[x](\\javascript:boom())",
        "[x](<javascript:boom()>)", "[x](data:text/html;base64,PHNjcmlwdD5ib29tKCk8L3NjcmlwdD4=)", "[x](vbscript:boom)",
        "[x]\n\n[x]: javascript:boom()", "[x][y]\n\n[y]: data:text/html,<script>boom()</script>",
        "![x](javascript:boom())", "![x](data:image/svg+xml,<svg onload=boom()>)", "![x](https://e.test/a.png\"onerror=\"boom())",
        "![x\"onerror=\"boom()](/a.png)", "![x](/a.mp4\" onloadstart=\"boom())", "[x](https://e.test \"t\" onmouseover=\"boom()\")",
        "[x](https://e.test \"t\\\" onmouseover=\\\"boom()\")", "<https://e.test/\"onmouseover=\"boom()>", "<javascript:boom()>",
        "https://e.test/\"onmouseover=\"boom()", "www.e.test/\"onmouseover=\"boom()", "<x@e.test\" onmouseover=\"boom()>",
        "```\"><script>boom()</script>\nx\n```", "```js onclick=boom()\nx\n```", "~~~\"><img src=x onerror=boom()>\nx\n~~~",
        "| a |\n|---|\n| <img src=x onerror=boom()> |", "| <script>boom()</script> |\n|:-:|\n| x |",
        "- [ ] <script>boom()</script>", "> <script>boom()</script>", "# <script>boom()</script>",
        "`<script>boom()</script>`", "*<script>boom()</script>*", "**<img src=x onerror=boom()>**",
        "&lt;script&gt;boom()&lt;/script&gt;", "&#60;script&#62;boom()&#60;/script&#62;", "&#x3c;img src=x onerror=boom()&#x3e;",
        "&amp;lt;script&amp;gt;", "\\<script>boom()\\</script>", "a\u0000<script>boom()</script>",
        "[a](<b)c>)", "[a](b(c)", "[[x](/y)](/z)", "![![x](/a.png)](/b.png)", "[![x](javascript:boom())](javascript:boom())",
        "1. <script>\n2. boom()\n   </script>", "    <script>boom()</script>", "***\n<script>boom()</script>\n***",
        "<!--\n<script>boom()</script>\n-->", "<![CDATA[<script>boom()</script>]]>", "<?php boom() ?>",
        "[x](" + new string('(', 500) + ")", new string('>', 600) + " <script>boom()</script>",
        string.Concat(Enumerable.Range(0, 80).Select(i => new string(' ', 2 * i) + "- <script>boom()</script>\n")),
    ];

    [Fact]
    public async Task Nothing_the_source_says_becomes_markup_outside_the_fixed_set()
    {
        if (NoBrowser) return;
        var sources = Cases().Select(c => c.Source).Concat(Hostile).ToArray();
        var page = await Open(Fixture);

        var offences = await page.EvaluateAsync<string[]>("""
            sources => {
                const quiet = [];
                const attributes = {
                    A: ['href', 'title', 'target', 'rel'], IMG: ['src', 'alt', 'title', 'loading'],
                    VIDEO: ['controls', 'preload', 'src', 'aria-label', 'title'], OL: ['start'],
                    LI: ['class'], INPUT: ['type', 'disabled', 'checked'], TH: ['align'], TD: ['align'],
                    TABLE: ['role', 'tabindex'], CODE: ['class'],
                };
                const bare = ['UL', 'P', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'BLOCKQUOTE', 'HR', 'PRE', 'EM', 'STRONG', 'DEL',
                    'BR', 'THEAD', 'TBODY', 'TR'];
                const keptAddress = /^(?:https?:|mailto:|tel:|[^:\/?#]*(?:[\/?#]|$))/i;
                const found = [];
                for (const src of sources) {
                    const t = document.createElement('template');
                    t.innerHTML = sednaUi.md.render(src);
                    for (const el of t.content.querySelectorAll('*')) {
                        const allowed = el.tagName in attributes ? attributes[el.tagName] : bare.includes(el.tagName) ? [] : null;
                        if (allowed === null) { found.push(`<${el.tagName}> from ${JSON.stringify(src).slice(0, 80)}`); continue; }
                        for (const a of el.attributes) {
                            const where = `${el.tagName}[${a.name}] from ${JSON.stringify(src).slice(0, 80)}`;
                            if (!allowed.includes(a.name)) found.push(where);
                            else if ((a.name === 'href' || a.name === 'src') && !keptAddress.test(a.value.trim())) found.push(where + ' = ' + a.value);
                            else if (a.name === 'class' && !/^(?:language-[\w#+.\-]+|task-list-item)$/.test(a.value)) found.push(where + ' = ' + a.value);
                        }
                        if (el.tagName === 'A' && el.getAttribute('target') !== '_blank') found.push('link without target');
                        if (el.tagName === 'A' && !/noopener/.test(el.getAttribute('rel') || '')) found.push('link without noopener');
                    }
                }
                return found;
            }
            """, sources);

        Assert.True(offences.Length == 0, "Markup the renderer must never write:\n  " + string.Join("\n  ", offences));
    }

    [Fact]
    public async Task Source_nested_far_deeper_than_anything_written_does_not_stop_the_page()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        // Each of these once meant a call stack as deep as the source, or time that grows with
        // its square: they must finish, and finish quickly.
        var timings = await page.EvaluateAsync<Dictionary<string, double>>("""
            () => {
                const run = {
                    quotes: '>'.repeat(20000) + ' a',
                    lists: Array.from({ length: 3000 }, (_, i) => ' '.repeat(2 * i) + '- a').join('\n'),
                    brackets: '['.repeat(50000),
                    closed: '['.repeat(20000) + ']'.repeat(20000),
                    emphasis: '*a '.repeat(50000),
                    nested: '*_'.repeat(8000) + 'x' + '_*'.repeat(8000),
                    ticks: Array.from({ length: 3000 }, (_, i) => '`'.repeat(i % 80 + 1) + ' a').join(' '),
                    links: '['.repeat(4000) + 'x' + '](/a)'.repeat(4000),
                    lines: 'line\n'.repeat(60000),
                    table: '| a | b |\n|---|---|\n' + '| 1 | 2 |\n'.repeat(20000),
                };
                const out = {};
                for (const [name, src] of Object.entries(run)) {
                    const t = performance.now();
                    sednaUi.md.render(src);
                    out[name] = performance.now() - t;
                }
                return out;
            }
            """);

        foreach (var (name, ms) in timings) Assert.True(ms < 2500, $"{name} took {ms:0} ms");
    }

    [Fact]
    public async Task A_reference_defined_below_the_paragraph_that_uses_it_still_links()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        Assert.Equal("https://example.com/orders",
            await page.EvaluateAsync<string>("""
                () => { const t = document.createElement('template');
                        t.innerHTML = sednaUi.md.render('See [the orders][o] and [o].\n\n[o]: https://example.com/orders');
                        return t.content.querySelectorAll('a')[1].getAttribute('href'); }
                """));
    }

    [Fact]
    public async Task The_preview_of_a_document_reads_as_one_in_the_page()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        var text = await page.EvaluateAsync<string>("""
            () => { const el = document.getElementById('out');
                    el.className = 'markdown-body';
                    el.innerHTML = sednaUi.md.render('# T\n\n- [x] done\n- [ ] open\n\n> quote\n\n| a | b |\n|:-:|--:|\n| 1 | 2 |');
                    return [...el.children].map(c => c.tagName).join(','); }
            """);

        Assert.Equal("H1,UL,BLOCKQUOTE,TABLE", text);
    }
}
