![Sedna.UI — one design-token contract. Semantic CSS. Consistent Blazor apps.](https://raw.githubusercontent.com/dennisrahmen/Sedna.UI/main/assets/brand/dr-simple-ui-social-preview.png)

[![CI](https://img.shields.io/github/actions/workflow/status/dennisrahmen/Sedna.UI/ci.yml?branch=main&logo=github&style=flat-square&label=ci)](https://github.com/dennisrahmen/Sedna.UI/actions/workflows/ci.yml)
[![Catalogue](https://img.shields.io/badge/catalogue-browse-FF6B4A?style=flat-square&logo=github)](https://www.sedna-ui.com/)
[![NuGet version](https://img.shields.io/nuget/v/Sedna.UI?color=FF6B4A&label=nuget&logo=nuget&style=flat-square)](https://www.nuget.org/packages/Sedna.UI/)
[![NuGet downloads](https://img.shields.io/nuget/dt/Sedna.UI?color=FF6B4A&label=downloads&logo=nuget&style=flat-square)](https://www.nuget.org/packages/Sedna.UI/)
[![.NET](https://img.shields.io/badge/.NET-10.0-FF6B4A?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Licence](https://img.shields.io/github/license/dennisrahmen/Sedna.UI?color=FF6B4A&logo=github&style=flat-square)](https://github.com/dennisrahmen/Sedna.UI/blob/main/LICENSE)
[![Stars](https://img.shields.io/github/stars/dennisrahmen/Sedna.UI?color=FF6B4A&logo=github&style=flat-square)](https://github.com/dennisrahmen/Sedna.UI/stargazers)

# Sedna.UI

**One design-token contract. Semantic CSS. Consistent Blazor apps.**

A shared UI layer for .NET 10 / Blazor apps.

```bash
dotnet add package Sedna.UI
```

## Two tiers

**The frame** — shell, sidebar, header, user widget — is chrome that must be pixel-identical in every app
and that nobody should restyle per project.

**The paint** — tables, forms, cards, badges, buttons, alerts — is what a page puts inside it.

**Both are semantic CSS classes.** Pages write plain, open HTML and apply them, so there is no
`<DataTable>`, no `<AppShell>`, and there will not be either. The package is the stylesheet, the script,
the icons and a small C# surface for what markup cannot express — which link is the current page, and
typed access to the browser API.

→ [Architecture and the token contract](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/architecture.md)

## Documentation

| Link | Summary |
|---|---|
| [Getting started](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/getting-started.md) | Install, host-page setup, rebranding, icon font |
| [Architecture](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/architecture.md) | The two tiers, the token contract, theming, z-order |
| [Releasing](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/releasing.md) | SemVer rules, trusted publishing setup |
| [Development](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/development.md) | Build, test, the guard tests, package verification |
| [Consuming-app `CLAUDE.md`](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/CLAUDE.consuming-app.md) | Drop-in rules for an app that uses this |
| [Migrating from DR.Simple_UI](https://github.com/dennisrahmen/Sedna.UI/blob/main/docs/migrating-to-sedna-ui.md) | Upgrading from the old package ID and brand |

**The catalogue** — every class with a page of copy-pasteable HTML, at
**[www.sedna-ui.com](https://www.sedna-ui.com/)**. It is a Blazor app built with this
library, so an example that renders correctly there renders correctly in yours.

**An MCP server for AI agents** — `https://www.sedna-ui.com/mcp`. One URL, and an agent can
search the catalogue, read what a class does and copy the exact markup. Every result names the release
that introduced it, so an agent can check before copying something the installed version does not have.

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