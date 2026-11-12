# `Sedna.UI.Catalogue/` — the hosted catalogue and the MCP server

A Blazor Web App with global interactive server rendering, deployed to
<https://www.sedna-ui.com/>. It is **not** packaged: one package ships from this repo and it is
the library.

This site is an application built with Sedna.UI, not a site describing one. Its shell, sidebar,
topbar and page are the library's own frame classes written by hand — so a regression in the frame shows
up here first, and reading this project shows how the library is actually used.

## Adding a page

1. `Components/Pages/<Name>.razor`, with `@page "/<route>"` and a `<CatHead>`.
2. One file per example under `Examples/<Name>/`.
3. Register the page in `Navigation/CataloguePages.cs`.
4. Give it an entry in `docs/design-guide/components/<group>.json` and run
   `build/guide-stamp.sh /<route>` — `docs/design-guide/README.md` has the schema. The layout shows the
   entry at the foot of the page by itself.
5. `dotnet test`. An orphaned page, a registry entry with no page and a page with no guide entry all
   fail.

## Examples

**Write each example once, as a file.** The page renders it *and* prints it, from the same embedded
bytes, so a demo and its snippet cannot drift.

```razor
<CatExample TExample="Examples.Badge.Semantic" Title="Semantic">
    <p>One or two sentences: when to use this, and what the reader would otherwise get wrong.</p>
</CatExample>
```

- **A live example is a `.razor` file** containing **plain HTML and no Razor syntax at all**. Then the
  bytes on disk are the bytes compiled, rendered and printed — nothing is escaped, so the snippet pastes
  into a `.razor` page and an `.html` file alike. `ExampleSourceTests` fails on any `@` that is not part
  of an e-mail address.
- **A code-only snippet is a `.html`, `.css` or `.txt` file**, rendered by `<CatSnippet Name="…">`. It
  must **never** be named `.razor`: the Razor SDK's own `<Content Include="**\*.razor">` glob would
  sweep it into a component and the build would fail somewhere confusing. No two-dot filenames either —
  that breaks default manifest naming.
- **`Examples/Interop/` is the one place Razor is allowed** — for demonstrating the C# surface:
  `ISednaUi`, `ISednaOverlays`, `ISednaGraphs`, `ISednaEditors`, `ActiveLink`, a binding — where the
  thing being shown *is* C#. A test asserts from both directions that it is used for nothing else, so it
  cannot become a home for examples that failed the scan.
- **An example that needs a second file** — the app's own overlay component, for an example that
  presents one — names it with `Companion="Interop/ReassignOrder.razor"`. The page shows the two files as
  tabs in a card, and the MCP index files the companion under the same example's title. It is never
  rendered on its own.
- `Demo="ex-demo--block"` lays the demo out as a block instead of a centred row; add `ex-demo--pad` for
  something that needs room, or `ex-demo--flush` for a demo that brings its own edge, like a whole
  frame. Omit it for small inline things like a badge.
- `Shell="Orders"` draws the demo inside the app's frame (`Components/Docs/CatShell.razor`), with the
  example as the content of `.page` and the named sidebar link marked current; `ShellHeight` fixes the
  frame's height for a recipe whose point is something that sticks, and `AuthShell` uses
  `.auth-layout` instead. The code block still prints only the example. It is for a whole page — the
  page recipes — and the example holds no frame markup of its own.
- **A `Title` is a short topic name**: sentence case, at most six words, no leading "A", "An" or
  "The", and no code — the class or member the example teaches goes in the sentence under it, in
  `<code>`. It is also the anchor slug, the accessible name, and what the MCP server returns.
  `SectionHeadingTests` holds every heading to this.
- **A long page is grouped into topics.** `<CatGroup Title="Splits">` wraps the examples of one
  topic, with an optional sentence before them; inside it an example's heading is an `h3`, a
  hand-written section is `<h3 id="…">` with its `CatAnchor`, and the contents beside the page nest
  them under the topic. More than eight sections at the top of a page fail the test, and a group
  holds two or more. On a grouped page every section sits in a group.

**Every folder under `Examples/` must be a valid C# identifier** — `ButtonGroup`, not `button-group`.
MSBuild builds the manifest resource name from the folder path and the Razor SDK builds the component
namespace from the same path by different code; a hyphen desynchronises them, and the symptom is a blank
code block on one page rather than a build error. `RootNamespace` is set explicitly in the csproj for
the same reason.

## The app's own JavaScript reads, and never writes to the DOM

Blazor owns the document under global interactivity. Anything `catalogue.js` mutated would be reverted
the next time that subtree re-rendered — silently, and only sometimes. So every function there returns
data, C# renders it, and the two handlers are the exceptions: one moves focus, and one cancels the
click on a demo's `href="#"` placeholder. Neither is DOM state.

The library's own script is exempt and always was: it delegates every handler from `document`, so it
survives re-renders by construction.

## Rules

- **Single source of CSS.** The app serves `_content/Sedna.UI/…`, never a copy. `HostPageTests`
  compares the bytes the running app returns against the file in the repo.
- **`Components/App.razor` is the block in `docs/getting-started.md`**, verbatim, with `catalogue.css`
  standing in for a consuming app's `brand.css`. Plain hrefs, not `@Assets[…]` — a fingerprinted URL
  would make the page that documents the load order differ from the page anyone can paste. A test
  asserts the two agree, so the documented block is executed on every CI run.
- **`wwwroot/catalogue.css` may only style `.cat-*` and `.ex-*`.** It is the docs' own chrome; styling
  anything else would make an example look better here than in the app that copies it.
- **Inside `.cat-main`, `.cat-ex` or `.ex-demo`, reach a bare element with `>` and never with a
  descendant selector.** This file is **unlayered**, so it outranks every `@layer sedna.*` rule
  whatever the two selectors weigh — `.cat-main h3` restyled the `h3` in every `.modal-header`,
  `.drawer-header`, `.markdown-body` and `.prose` an example renders, and a modal header measured
  111px here against 61px in an app. A page heading is always a direct child; an example's markup
  never is.
- **`/health` asserts the host page's assets, and is Railway's healthcheck**, so a publish that drops
  one fails the deploy instead of replacing a working site with a dead one. It reads the paths out of
  `Components/App.razor` — add an asset to the host page and it is covered; never write the list out.
- **Same-page links, under `<base href="/">`.** A bare `href="#id"` resolves against the root and opens
  the landing page, unless `Sedna.UI.js` handles it — which it does for a target on the page, moving
  focus. So:
  - a heading's link icon, which is meant to be copied, carries the page's path: `<CatAnchor Id="…" />`,
    checked by `SectionAnchorTests`;
  - a jump — the skip link, a validation summary — is a bare fragment, checked by `SamePageLinkTests`;
  - in an example, a fragment names an id the same example declares, or is the placeholder `#`, which
    `catalogue.js` cancels inside a demo. `ExampleSourceTests` fails on any other.
- **Every page has its contents beside it**, built from its sections by `Navigation/PageContents.cs`.
  A section is a `CatGroup`, a `CatExample`, a `CatSnippet`, or a hand-written `<h2 id="…">` — `<h3>`
  inside a group — with a `<CatAnchor Id="…" />` in it — the same id, and every page heading has
  one, so its link can be copied. A heading written in a loop has no literal id to read: the page's sections are then
  added in `PageContents` from the data the loop renders, as the principles' and the tokens' are —
  each nested under its topic, from `PrincipleTopics` and `TokenGroups.Sections`. `PageContentsTests` fails when a page's contents and its anchored headings disagree.
- **An unknown address renders `Components/Pages/NotFound.razor`** with a 404, through the router's
  `NotFoundPage` and `UseStatusCodePagesWithReExecute`. Its route is `CataloguePages.NotFoundRoute` and
  it is not in `All`; `NotFoundTests` covers it.
- **A page's search and preview metadata comes from the registry.** `Navigation/SiteMeta.cs` derives
  the description, canonical, Open Graph and X card, JSON-LD, `/sitemap.xml` and `/robots.txt` from
  `CataloguePages`; `Components/Layout/CatalogueHead.razor` writes them into the head. **A page's
  `Blurb` is its search description** — one sentence, short enough that `SiteMetaTests` keeps the result
  under 160 characters. An address that is no registered page is `noindex`. Nothing is hand-listed, so
  registering a page is the whole job.
- **`z-index` comes from the documented scale** in `docs/architecture.md`, here too.
- **One remote host, and only for a photograph.** `images.unsplash.com`, named once in
  `CatalogueAssets.PhotoHost` and allowed in a page and in an example. Nothing else, and nothing at
  all in the library: the package's guarantee is that no host's outage can affect a customer's site.
  The catalogue is a web server we operate whose job is to teach, and `/media` cannot teach how an
  `<img>` is handled with no image in it — no CSS-drawn placeholder demonstrates `object-fit`.
  Three tests know about it: the two example guards in `ExampleSourceTests` and the console-error
  check in `PageLoadTests`, which ignores a failure from that host rather than failing the suite on
  somebody else's uptime. **Credit the photographer where the photograph is**, which is what
  Unsplash asks for and what a `figcaption` is.
- **No text is capped at a measure.** The content column is the measure; a `max-width` in `ch` or
  `em` stops a paragraph half way across while the demo beside it runs the full width.
  `CatalogueCssTests` fails on one.
- **The examples are the documentation.** Prefer realistic content — a real-sounding queue, an actual
  error message — over `Foo` and `Lorem ipsum`. Say what a class is *for* and which mistake it avoids;
  do not narrate the CSS, which the reader can read.

## Interactivity

Global `InteractiveServer`, with one invariant: **every example demo and every code block renders
identically before the circuit connects.** Interactivity adds behaviour; it is never required to see an
example. Three things genuinely need it — the `ISednaUi` demos on `/interop`, the theme toggles, and
the sidebar's active link.

The trap to know: with prerendering, the server's HTML is replaced when the circuit connects, so
anything that mutated the DOM during parse is lost. The read-only rule above is what keeps this project
immune.

## The MCP server

`Mcp/` — read-only tools and four resources at `/mcp`. `get_guide`, the goal hits of `search` and the
entry `get_page` returns read the design guide from `Guide/DesignGuide.cs`, which embeds
`docs/design-guide/` as the pages do. **There must never be a tool that writes.** The endpoint is public and unauthenticated, and a client honouring the read-only hint calls
these without prompting.

Nothing in the index is hand-listed: examples come from the same embedded resources the pages render,
classes from the stylesheet the app serves, docs from the repository's own `docs/`. `since` comes from
`build/class-history.sh` — the copy generated when the app is built (never committed), and for
anything it still calls unreleased, the latest release's own copy, which `ReleasedHistory` fetches from the GitHub release at runtime. The contract is in `docs/architecture.md`.

**No tool declares an `outputSchema`.** They return anonymous objects the SDK cannot describe, so
`UseStructuredContent` made each one advertise `{"type":"object","properties":{"result":true}}` — a
boolean subschema, legal JSON Schema and rejected by the MCP TypeScript SDK's Zod validator, which
dropped every tool in every client that validates a tool list while the server stayed connected and
its instructions loaded. A test fails on any boolean subschema a tool publishes.

**Every rejection is an `McpException`.** Its message is the only one the SDK propagates; any other
exception type reaches the caller as the bare `An error occurred invoking 'x'.` and leaves a model
retrying the same call. A rejection names the limit it broke or lists the values the argument accepts —
and an unknown enum value is a rejection, never an empty result.
