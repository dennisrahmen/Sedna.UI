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
4. `dotnet test`. An orphaned page and a registry entry with no page both fail.

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
- **`Examples/Interop/` is the one place Razor is allowed**, for demonstrating `ISednaUi` and
  `ActiveLink`, where the thing being shown *is* C#. A test asserts from both directions that it is used
  for nothing else, so it cannot become a home for examples that failed the scan.
- `Demo="ex-demo--block"` lays the demo out as a block instead of a centred row; add `ex-demo--pad` for
  something that needs room. Omit it for small inline things like a badge.
- A backtick in a `Title` renders as `<code>`. `Title` stays a plain string because it is also the
  anchor slug, the accessible name, and what the MCP server returns.

**Every folder under `Examples/` must be a valid C# identifier** — `ButtonGroup`, not `button-group`.
MSBuild builds the manifest resource name from the folder path and the Razor SDK builds the component
namespace from the same path by different code; a hyphen desynchronises them, and the symptom is a blank
code block on one page rather than a build error. `RootNamespace` is set explicitly in the csproj for
the same reason.

## The app's own JavaScript reads, and never writes to the DOM

Blazor owns the document under global interactivity. Anything `catalogue.js` mutated would be reverted
the next time that subtree re-rendered — silently, and only sometimes. So every function there returns
data, C# renders it, and the one exception moves focus, which is not DOM state.

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
- **`z-index` comes from the documented scale** in `docs/architecture.md`, here too.
- **Nothing is loaded from a remote host**, in a page or in an example.
- **The examples are the documentation.** Prefer realistic content — a real-sounding queue, an actual
  error message — over `Foo` and `Lorem ipsum`. Say what a class is *for* and which mistake it avoids;
  do not narrate the CSS, which the reader can read.

## Interactivity

Global `InteractiveServer`, with one invariant: **every example demo and every code block renders
identically before the circuit connects.** Interactivity adds behaviour; it is never required to see an
example. Three things genuinely need it — the `ISednaUi` demos on `/script`, the theme toggles, and
the sidebar's active link.

The trap to know: with prerendering, the server's HTML is replaced when the circuit connects, so
anything that mutated the DOM during parse is lost. The read-only rule above is what keeps this project
immune.

## The MCP server

`Mcp/` — six read-only tools and four resources at `/mcp`. **There must never be a seventh tool that
writes.** The endpoint is public and unauthenticated, and a client honouring the read-only hint calls
these without prompting.

Nothing in the index is hand-listed: examples come from the same embedded resources the pages render,
classes from the stylesheet the app serves, docs from the repository's own `docs/`. `since` comes from
`build/class-history.sh`. The contract is in `docs/architecture.md`.
