# Contributing

## Requesting a token, class or variant

Apps that consume `Sedna.UI` cannot release it, so a missing value is a request rather than a local
fix. [Open an issue](https://github.com/dennisrahmen/Sedna.UI/issues) with:

- what you need — a colour role, a button variant, a class
- where it is used, and what you would otherwise write locally
- whether an existing token or class nearly fits

Adding a token, class or variant is a minor release. Renaming or removing one is major, so requests to
rename are weighed against every app already using the old name.

A release can also be major with nothing renamed or removed: changing an existing rule enough to move
layout or colour, and making an existing app override stop working, both count. So does adding a class
whose name a consuming app already styles — the two sets of declarations merge and the app's appearance
changes with no error. New class names are therefore checked against the apps known to consume this
package, and every release lists the names it adds.

Until a release lands, use an **app-prefixed** variable (`--myapp-…`) in your own stylesheet rather than a
new bare `--` name, which a future library version could claim with a different meaning. Do not override
library classes in an app; the next upgrade will fight you.

## Pull requests

By contributing you agree your contribution is licensed under [Apache-2.0](LICENSE), as stated in §5 of
that licence.

Before opening a PR:

```bash
dotnet build Sedna.UI.slnx
dotnet test  Sedna.UI.slnx
```

The tests are source scans that enforce the design rules, not behaviour checks. If one fails it is
telling you the change breaks a documented contract — read the failure message before working around it.
See [docs/development.md](docs/development.md) for what each guard covers.

### What will be accepted

- New tokens, classes, variants and catalogue pages.
- Fixes to existing values, accessibility and browser behaviour.
- Documentation.

### What will not

- **Components that wrap page content.** Tables, forms, cards and panels are CSS classes on purpose. A
  `<DataTable>` hides markup from whoever edits the page. See
  [docs/architecture.md](docs/architecture.md).
- **A dependency on MudBlazor, Syncfusion, Radzen or Tailwind.**
- **A second icon set.** Remix Icon is bundled and is the only one.
- **Colour literals in the library CSS.** Every colour resolves through a token; a test enforces it.
- **App-specific business UI** — approval panels, SLA badges, tour overlays, page-specific grids.
- **Anything the package loads from a remote URL at runtime.** Everything it needs ships inside it.

## Adding a class

1. Add the rule to the owning part under `src/Sedna.UI/css-parts/` (or a new `NN`-prefixed part, per
   that directory's own `CLAUDE.md`), referencing tokens for every colour, then re-run
   `build/bundle-css.sh`. Never edit `src/Sedna.UI/wwwroot/css/Sedna.UI.css` directly — it is
   generated, and a guard test rejects a hand-edit.
2. Add or extend a catalogue page under `src/Sedna.UI.Catalogue/Components/Pages/`, with its
   example markup as a file under `Examples/`, and register the page in `CataloguePages`.
3. `dotnet test`.

Conventions — naming, the token contract, the two tiers — are in [CLAUDE.md](CLAUDE.md). It is written for
both humans and AI agents working in this repo.

## Reporting a bug

Include the library version, the browser, and the smallest markup that reproduces it. If it is visual,
state which theme (`data-theme`, `data-cvd`, `data-density`) it appears under: a visual defect is often
theme-specific.

## Maintainers

Release process, versioning rules and publishing setup are in
[docs/releasing.md](docs/releasing.md).
