using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The <c>hidden</c> attribute hides every element the stylesheet gives a display of its
/// own.
/// </summary>
/// <remarks>
/// <para>
/// The browser hides a <c>[hidden]</c> element with a user-agent rule, and any author rule
/// that sets <c>display</c> outranks it whatever the specificity. So <c>.alert</c>'s
/// <c>display: flex</c> left an alert an app had marked <c>hidden</c> on the page, and the
/// library had patched the same gap family by family — the toast, the banner, the menu
/// item, the combo option — each with a <c>[hidden]</c> rule of its own, and every family
/// without one still had it.
/// </para>
/// <para>
/// One rule in <c>sedna.utilities</c> answers it for all of them: that layer comes after
/// every component, so it wins whatever a component's selector weighs. This test walks
/// every rule in the shipped stylesheet that sets a display, builds the markup its selector
/// describes, checks the selector matches it, then adds <c>hidden</c> and asserts the
/// element is not rendered — so a rule added in a later layer, or one that out-weighs the
/// general rule, fails here.
/// </para>
/// </remarks>
public class HiddenAttributeTests : ScriptTestBase
{
    private const string Walk =
        """
        () => {
          const sheet = [...document.styleSheets].find(s => s.href && s.href.endsWith('/css/Sedna.UI.css'));
          const rules = [];
          const visit = list => {
            for (const r of list) {
              if (r instanceof CSSStyleRule) { if (r.style.display && r.style.display !== 'none') rules.push(r.selectorText); }
              else if (r.cssRules && !(r instanceof CSSKeyframesRule)) visit(r.cssRules);
            }
          };
          visit(sheet.cssRules);

          // Splits at top-level commas or combinators, ignoring those inside () and [].
          const topLevel = (text, at) => {
            const parts = []; let depth = 0, start = 0;
            for (let i = 0; i < text.length; i++) {
              const c = text[i];
              if (c === '(' || c === '[') depth++;
              else if (c === ')' || c === ']') depth--;
              else if (depth === 0 && at(c, i)) { parts.push(text.slice(start, i)); start = i + 1; }
            }
            parts.push(text.slice(start));
            return parts.map(p => p.trim()).filter(p => p.length);
          };

          // A compound as an element: its tag, classes, id and attribute selectors. Pseudo-
          // classes are left to the match check below.
          const element = compound => {
            const bare = compound.replace(/:{1,2}[\w-]+(\((?:[^()]|\([^()]*\))*\))?/g, '');
            const tag = (bare.match(/^[a-z][\w-]*/i) || ['div'])[0];
            const el = document.createElement(tag);
            for (const m of bare.matchAll(/\.([\w-]+)/g)) el.classList.add(m[1]);
            const id = bare.match(/#([\w-]+)/); if (id) el.id = id[1];
            for (const m of bare.matchAll(/\[([\w-]+)(?:([~|^$*]?=)\s*"?([^"\]]*)"?)?\]/g)) {
              el.setAttribute(m[1], m[3] ?? '');
            }
            return el;
          };

          const host = document.getElementById('host');
          const failures = [], unbuilt = [];
          let checked = 0;
          for (const selectorText of rules) {
            for (const selector of topLevel(selectorText, c => c === ',')) {
              if (selector.includes('::') || /^(:root|html|body)\b/.test(selector)) { unbuilt.push(selector); continue; }
              // Compounds and the combinators between them, at the top level only.
              const tokens = []; let depth = 0, current = '';
              for (const c of selector) {
                if (c === '(' || c === '[') depth++;
                else if (c === ')' || c === ']') depth--;
                if (depth === 0 && (c === ' ' || c === '>' || c === '+' || c === '~')) {
                  if (current) tokens.push(current);
                  current = '';
                  if (c !== ' ') tokens.push(c);
                } else current += c;
              }
              if (current) tokens.push(current);
              let parent = host, prev = null, combinator = ' ', subject = null;
              for (const token of tokens) {
                if (token === '>' || token === '+' || token === '~') { combinator = token; continue; }
                const el = element(token);
                if (prev && (combinator === '+' || combinator === '~')) prev.after(el);
                else (prev ?? parent).append(el);
                prev = el; subject = el; combinator = ' ';
              }
              let matches = false;
              try { matches = subject.matches(selector); } catch { matches = false; }
              if (!matches || getComputedStyle(subject).display === 'none') { unbuilt.push(selector); host.replaceChildren(); continue; }
              subject.hidden = true;
              const shown = getComputedStyle(subject).display;
              if (shown !== 'none') failures.push(`${selector} → display: ${shown}`);
              checked++;
              host.replaceChildren();
            }
          }
          return { checked, failures, unbuilt: unbuilt.length };
        }
        """;

    private sealed class Result
    {
        public int Checked { get; set; }
        public string[] Failures { get; set; } = [];
        public int Unbuilt { get; set; }
    }

    [Fact]
    public async Task Every_element_the_stylesheet_displays_is_hidden_by_the_hidden_attribute()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""<div id="host"></div>""");

        var result = await page.EvaluateAsync<Result>(Walk);

        // The vacuity guard: a walk that stopped building markup would pass on nothing.
        Assert.True(result.Checked > 300,
            $"Only {result.Checked} display rules were built and checked ({result.Unbuilt} could not be built).");
        Assert.True(result.Failures.Length == 0,
            $"{result.Failures.Length} rules keep an element on the page when it carries the hidden attribute:"
            + Environment.NewLine + string.Join(Environment.NewLine, result.Failures.Distinct()));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Until_found_still_leaves_the_content_findable()
    {
        // hidden="until-found" is the browser's own collapsed-but-searchable state: it is
        // drawn with content-visibility, not display, and a display of none would take the
        // content out of find-in-page.
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""<div class="alert" id="a" hidden="until-found">Order ORD-4211 is on hold.</div>""");

        var display = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('a')).display");

        Assert.NotEqual("none", display);
        Assert.Empty(errors);
    }
}
