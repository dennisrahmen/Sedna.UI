![Sedna.UI — semantic CSS for Blazor, built to be written by an agent.](https://raw.githubusercontent.com/dennisrahmen/Sedna.UI/main/assets/brand/sedna-ui-social-preview.png)

[![Release](https://img.shields.io/github/actions/workflow/status/dennisrahmen/Sedna.UI/release.yml?logo=github&style=flat-square&label=release)](https://github.com/dennisrahmen/Sedna.UI/actions/workflows/release.yml)
[![Catalogue](https://img.shields.io/badge/catalogue-browse-FF6B4A?style=flat-square&logo=github)](https://www.sedna-ui.com/)
[![NuGet version](https://img.shields.io/nuget/v/Sedna.UI?color=FF6B4A&label=nuget&logo=nuget&style=flat-square)](https://www.nuget.org/packages/Sedna.UI/)
[![NuGet downloads](https://img.shields.io/nuget/dt/Sedna.UI?color=FF6B4A&label=downloads&logo=nuget&style=flat-square)](https://www.nuget.org/packages/Sedna.UI/)
[![.NET](https://img.shields.io/badge/.NET-10.0-FF6B4A?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Licence](https://img.shields.io/github/license/dennisrahmen/Sedna.UI?color=FF6B4A&logo=github&style=flat-square)](https://github.com/dennisrahmen/Sedna.UI/blob/main/LICENSE)
[![Stars](https://img.shields.io/github/stars/dennisrahmen/Sedna.UI?color=FF6B4A&logo=github&style=flat-square)](https://github.com/dennisrahmen/Sedna.UI/stargazers)

# Sedna.UI

**Semantic CSS for Blazor, built to be written by an agent.**

Every component library gets you to ninety percent. The last ten is the button the component has no
parameter for, and it is where you copy the component's markup out and rebuild it yourself. One line of
component is also a few hundred lines of HTML that neither you nor your agent can see without reading
its source.

**Sedna.UI starts you at a hundred.** The markup is yours from the first line: plain, open HTML with
semantic classes. The package ships what sits underneath — the stylesheet, a design-token contract, the
icons and the script.

**Classes, and the behaviour with them.** Utility CSS leaves every dropdown and dialog to you. Here
menus, tabs, popovers and the command palette work from attributes on your own markup, a toast is one
call, and a small C# surface opens the dialogs your app writes and awaits their result.

**Your agent reads the catalogue you do.** One MCP URL, `https://www.sedna-ui.com/mcp`, and an agent
can search every class, copy the exact markup, and check which release introduced it before using it.

**A system, not a starter kit.** Every colour resolves through a token, every class has a catalogue page
of copy-pasteable HTML, and the catalogue is itself a Blazor app built with the library — so an example
that renders correctly there renders correctly in yours. The graph and the rich-text editor are held to
the same rules.

```bash
dotnet add package Sedna.UI
```

## Three tiers

**The frame** — shell, sidebar, header, user widget — is chrome that must be pixel-identical in every app
and that nobody should restyle per project.

**The paint** — tables, forms, cards, badges, buttons, alerts — is what a page puts inside it.

**The surfaces** — a toast, a hover hint, a graph, a rich-text editor — are what the library draws, because there is
nothing for the app to write. Everything around one is paint.

**The frame and the paint are semantic CSS classes.** Pages write plain, open HTML and apply them, so there is no
`<DataTable>`, no `<AppShell>`, and there will not be either. The package is the stylesheet, the script,
the icons and a small C# surface for what markup cannot express — which link is the current page, typed
access to the browser API, and a presenter that opens the dialogs an app writes. The markup on screen is
always the app's.

→ [Architecture and the token contract](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/architecture.md)

## Documentation

| Link | Summary |
|---|---|
| [Getting started](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/getting-started.md) | Install, host-page setup, rebranding, icon font |
| [Architecture](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/architecture.md) | The three tiers, the token contract, theming, z-order |
| [Graph](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/graph.md) | Records and the links between them on a canvas: markup, controls, events, C# |
| [Rich-text editor](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/editor.md) | Formatted text posted as HTML from a textarea: markup, toolbar, value, events, C# |
| [Surfaces](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/surfaces.md) | Tier 3: what the library draws, and the contract each surface meets |
| [Releasing](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/releasing.md) | SemVer rules, trusted publishing setup |
| [Development](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/development.md) | Build, test, the guard tests, package verification |
| [Consuming-app `CLAUDE.md`](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/CLAUDE.consuming-app.md) | Drop-in rules for an app that uses this |
| [Migrating from DR.Simple_UI](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/migrating-to-sedna-ui.md) | Upgrading from the old package ID and brand |

**The catalogue** — every class with a page of copy-pasteable HTML, at
**[www.sedna-ui.com](https://www.sedna-ui.com/)**.

**The MCP server** — `https://www.sedna-ui.com/mcp`, with its tools and client setup on
[the MCP server page](https://www.sedna-ui.com/mcp-server).

## Versioning

SemVer, driven by the git tag.

- **Major** — renaming or removing a token, class or shipped asset path, changing an existing rule
  enough to move layout or colour, or making an existing app override stop working.
- **Minor** — adding a token, class or variant.
- **Patch** — a fix that changes no contract.

Judged by what a consuming app sees, not by the size of the diff. When it is arguable, the higher level
wins.

→ [Full release process and SemVer rules](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/releasing.md)

## Icons

[Remix Icon](https://remixicon.com) is bundled — 3,245 icons, no CDN. Link
`_content/Sedna.UI/lib/remixicon/remixicon.css` and use `ri-*` classes.

## Contributing

Missing a token, class or variant? [Open an issue](https://github.com/dennisrahmen/Sedna.UI/issues).
Consuming apps cannot release this package, so a missing value is a request rather than a local override —
see [CONTRIBUTING.md](https://github.com/dennisrahmen/Sedna.UI/blob/main/CONTRIBUTING.md).

## Licence

[Apache-2.0](https://github.com/dennisrahmen/Sedna.UI/blob/main/LICENSE) — permissive, and usable in
closed-source and commercial applications.

This project uses icons from Remix Icon (<https://remixicon.com>), licensed under the Remix Icon License
v1.0. The icons are not covered by Apache-2.0; see
[THIRD-PARTY-NOTICES.md](https://github.com/dennisrahmen/Sedna.UI/blob/main/THIRD-PARTY-NOTICES.md).