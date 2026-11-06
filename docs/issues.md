# Outside requests

How issues, feature requests, pull requests, comments and suggestions from anyone other than the owner
are judged, built, answered and closed. An agent working through incoming issues follows this page and
the rules it points to; the owner does not review every verdict.

## Who may request

Anyone. Every request is judged on the rules and on whether the need is general, never on who asks or
how many apps they run. "Where it is used" is evidence for a request, not a condition for one.

## What an agent decides alone

Everything the rules settle: accepting, reshaping into a system, declining an app-specific or forbidden
request, implementing, making a breaking change that a system needs, naming classes, replying,
labelling and closing.

Ask the owner first, and post a short holding reply on the issue meanwhile, for:

- a request that is good but collides with a rule in `CLAUDE.md` or `docs/` — see
  [A rule that may be wrong](#a-rule-that-may-be-wrong);
- a new tier 3 surface or a new vendored engine — see [Surfaces and engines](#surfaces-and-engines);
- pushback that brings a new fact and would flip a verdict — see [Pushback](#pushback);
- a security report above the public line — see [Security](#security);
- anything this page and the rules do not settle.

An agent never merges an outside pull request, never tags a release and never force-pushes.

## Judging a request

Work through these in order and stop at the first that decides it.

1. **It already exists.** Reply with the one line that does it — an attribute, a token value, a
   `sednaUi` member — and add a catalogue example if nothing showed it. A request for something that
   exists but cannot be found is a gap in the catalogue.
2. **It is a defect.** Reproduce it on the current `main` in both engines. A component that lacks a
   state, a size or a token its family has is a defect, not a missing feature (`CLAUDE.md`, **Exact in
   every state**). Fix it with a test that fails without the fix.
3. **It lowers a floor.** The floors are not negotiable: WCAG AA contrast for text, control edges and
   focus rings; a keyboard path through everything a pointer does; 13px body text; both browser
   engines; forced colours; reduced motion. Decline the part below the floor and take the rest.
4. **A rule forbids it.** A component that wraps markup, library-drawn UI outside tier 3, a second
   engine or icon set, a remote load at runtime, a third-party package reference, anything under
   **Out of scope** in `CLAUDE.md`. Decline it — see [Declining](#declining).
5. **It is app-specific.** A class that knows what the app is for belongs to the app. Decline that
   part, and look for the general need underneath it: an app-written toast stack that loses the top
   layer is general, a notification card for one product's push flow is not. Accept the general need,
   and show how the app builds its own part from tier 2 classes and a class prefixed with its name.
6. **It is a fair gap.** Close it in the order `CLAUDE.md` gives — a token, then a variant, then a
   class — and then make it a system.

## A request becomes a system

Before building, check every component that could have the same gap. Where more than one has it, the
fix covers all of them, with a test that fails on the next component that misses it. Only a gap that
truly exists once is fixed narrowly.

Build what the request needs, not the shape it was asked in: a tone asked for one button family became
one tone system for every family that carries a tone, and a background asked for one card head became
component tokens for every family.

## Breaking changes

An agent may make a breaking change when a system needs it. Then, without exception:

- classify it by the version rules in `CLAUDE.md` — before 1.0.0 it ships in a minor bump;
- no alias, no fallback, no second name for the same idea;
- list every removed and renamed class at the top of the release notes;
- commit `docs/renames/<version>.json` for every renamed class, each old name with the class or
  classes that look the same;
- say in the issue reply that the release has breaking changes, listed at the top of its notes.

## Naming

The agent picks the name. Prefer a modifier on an existing block (`.media-frame--…`) or a token over a
new block, follow the family's existing scheme (tone names, `--sm` / `--lg`), and avoid a bare generic
word. Every release lists the classes it adds; that list is how a consuming app checks for a collision.

## Real names

An issue may name the requester's own apps, hosts, customers or colleagues. Never repeat any of them —
not in a reply, a commit, a branch name, a pull request, a test, an example, a doc or a comment. Write
"your app", or a generic role as **No real names** in `CLAUDE.md` describes. Leave the issue text as
the requester wrote it and do not raise it with them. Refer to a requester by their GitHub handle, and
only where a reply needs to.

## Replies

The agent posts replies itself. Every reply follows this shape:

- English, whatever language the issue is in.
- One line of thanks.
- The verdict in bold: **Accepted.**, **Accepted — confirmed bug.**, **Partly accepted.** or
  **Declined.**
- What will ship, named as the system it belongs to rather than as the one fix asked for.
- Where the requester's own workaround can go once the release is in.
- Breaking changes, if the release will have any.
- A version as "the next release" — never a version number or a date before the tag exists. After
  the release: "Released in [x.y.z](link to the release)".
- The footer `_🤖 Posted by [Claude Code](https://claude.com/claude-code)_`.

Post the verdict when it is made; do not wait for the release.

### Declining

Name the rule and link the doc that holds it. Show the shape the library does allow — markup with its
classes, a presenter that opens the app's own `<dialog>`, a `<template>` the script fills, a token the
app sets. Close the issue. No "maybe later".

## Labels and closing

| Label | When |
|---|---|
| `bug` | a confirmed defect |
| `enhancement` | an accepted gap |
| `duplicate` | another issue already tracks it |
| `wontfix` | declined |
| `question` | waiting on the requester |

- **Accepted** issues stay open until a published release contains them, then close with a "Released
  in" reply.
- **Declined** issues close with the verdict reply.
- **Duplicates** close with a link to the issue that tracks them.
- **Unclear or unreproducible** issues get one question asking for the library version, the browser,
  the theme and variant (`data-theme`, `data-variant`, `data-cvd`, `data-density`) and the smallest
  markup that shows it, and stay open. One still waiting 30 days after that question closes, saying it
  can be reopened.

## Pushback

When a requester argues a verdict again:

- **A new fact or use the verdict did not weigh:** judge it again. If the verdict would flip, ask the
  owner.
- **The same case repeated:** restate the rule once, politely, and do not reopen.

## A rule that may be wrong

When a request is good and a rule in `CLAUDE.md` or `docs/` stands in its way, do not change the rule.
Write the owner a short proposal — the rule, the request, what would change — and post a holding reply
on the issue. A rule change the owner approves is made in the doc that holds the rule.

## Surfaces and engines

A request for a new tier 3 surface or a new vendored engine is the owner's decision
([surfaces.md](surfaces.md#adding-a-surface)). Judge it against the surface contract and "one engine
per job", write up whether it could pass, and ask the owner. A request that plainly fails the contract
is declined like any forbidden request.

## Pull requests from outside

An outside pull request is judged as an issue and is never merged directly. Accepted work is rebuilt on
an agent branch in the library's shape — the system, the tests, the catalogue page, the `since` data —
with the author credited by a `Co-authored-by:` line using their GitHub handle and GitHub noreply
address. The outside pull request then closes with a link to the one that carries the work.

## Security

The line between a public issue and a private report is whether a page's visitor can exploit it.

- **Public issues are fine** for hardening that nothing a visitor sends can trigger: a stricter
  default, a missing CSP note, defence in depth.
- **Private reporting** ([`SECURITY.md`](../SECURITY.md)) for anything a visitor's input can exploit —
  script injection through the Markdown preview, the rich-text editor or any surface, a remote load —
  and for any problem in the catalogue's MCP server.

A report above that line that arrives as a public issue: do not discuss its details in the thread, and
ask the owner. The fix ships as a patch release with a GitHub security advisory.

## Shipping

Issues are worked through in rounds. Each round is one pull request; the owner merges it and confirms
the version and the notes before the tag (`CLAUDE.md`, **When asked to release**). A confirmed defect
with no workaround may ship alone as a patch.

## Records

The verdict reply on the issue is the record of what was decided and why. The repository keeps no
decision log.
