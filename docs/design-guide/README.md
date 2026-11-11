# The design guide

Which part of Sedna.UI answers what an app's user has to do, how it is combined with the rest, and
how a page is composed with it. This directory is the guide's one home: the catalogue's
[design guide page](https://www.sedna-ui.com/design-guide), the panel under every component page's
title, the topbar search and the MCP server's `search`, `get_guide` and `get_page` all read these
files, and nothing repeats them.

## The files

| File | Holds | Written by |
|---|---|---|
| `principles.md` | The general guide: composition, spacing, colour, actions, feedback, density, motion, words | hand |
| `components/<group>.json` | One entry per catalogue page in that sidebar group | hand, except `stamp` |
| `goals/<group>.json` | The user goals whose primary answer is a page in that group | hand |
| `stamps/<group>.json` | The sources each entry was last reviewed against | `build/guide-stamp.sh` — never edit |
| `pending.json` | The groups whose files are not written yet | hand; ends empty |

A group's file is named after its catalogue group in lowercase — `Navigation/CataloguePages.cs`
`Groups`: `start`, `frame`, `structure`, `actions`, `forms`, `data`, `labels`, `content`, `agents`,
`feedback`, `overlays`, `utilities`. `components/data.json` and `goals/data.json` are the model to
copy.

## The schema

Every key below is required, no other key is allowed, and none may be null. Text is plain — no
Markdown, no HTML — and names classes and attributes as they are written (`.col-num`,
`aria-sort`).

### An entry — `components/<group>.json`

An array, one object per catalogue page in the group, in the sidebar's order.

```json
{
  "page": "/table",
  "useFor": "One sentence: what the reader does with it.",
  "combineWith": [ { "page": "/toolbar#filters-and-a-count", "how": "Where it goes and why, one sentence." } ],
  "notFor": [ { "text": "The job it is not for, no full stop", "instead": "/card#key-value-rows" } ],
  "goals": [ "compare-records" ],
  "stamp": ""
}
```

- `page` — the page's route, exactly as registered, with no anchor.
- `useFor` — one sentence, ending with a full stop: the reader's job it answers, not what the CSS does.
- `combineWith` — at least one page it is used with; `how` is one sentence saying where it goes
  relative to this one.
- `notFor` — the jobs a reader would wrongly bring here, each with the page that does it. May be
  empty; usually it is not.
- `goals` — every goal whose `answer` names this page, in any role. May be empty.
- `stamp` — written by `build/guide-stamp.sh`. A new entry starts with `""`.

### A goal — `goals/<group>.json`

An array of the goals whose `primary` answer is a page in the group.

```json
{
  "id": "compare-records",
  "goal": "Let the user scan and compare many records in columns",
  "phrases": [ "data table", "data grid", "show records in a table" ],
  "answer": [
    { "page": "/table#with-a-toolbar-in-a-card", "role": "primary" },
    { "page": "/toolbar", "role": "part" }
  ],
  "how": "One to three sentences: the composition, and why it works.",
  "avoid": "The usual mistake, in one sentence."
}
```

- `id` — kebab-case, unique across every file. It is the goal's address: `get_guide("compare-records")`
  and `/design-guide#compare-records`.
- `goal` — starts `Let the user `, no full stop.
- `phrases` — at least three, lowercase: what a developer or an agent would type. Plain words,
  synonyms, and the names other libraries use — "data grid", "snackbar", "dropdown", "modal",
  "stepper". No phrase may belong to two goals.
- `answer` — exactly one `primary`, the page to start from; every other page is a `part`. An
  exempt page is never an answer.
- `how` and `avoid` — sentences ending with a full stop.

### References

Wherever a page is named — `combineWith`, `notFor.instead`, `answer` — it is a registered route,
optionally with `#anchor`. An anchor is an id on that page as the site serves it: an example's id
is its `Title` through `Components/Docs/Slug.cs` — lowercase words joined by hyphens, so
`Title="With a toolbar in a card"` is `#with-a-toolbar-in-a-card` — and a heading written with an
`id` is that id. Point at the example a reader should copy, not only at its page.

### Principles

`principles.md` is Markdown restricted to what the page renders: `## ` sections, `### ` headings,
paragraphs, `- ` lists, tables, `` `code` ``, `**bold**` and `[links](…)`. A section's id is its
heading through `Slug.cs`, and the page, `get_guide` and other documents link to it by that id, so
rename a heading only together with every link to it. Link a catalogue page by its address on
`https://www.sedna-ui.com/` and a document in `docs/` by its relative path; both then work on GitHub,
on the site and over MCP. Where another document already states a rule, link it rather than stating
it a second way.

## Pages without an entry

A page that documents no component has no entry and is listed, with its reason, in
`GuideFiles.Exempt` in `src/Sedna.UI.Catalogue.Tests/TestSupport/GuideFiles.cs`: the overview,
getting started, the design guide itself, concepts, tokens, branding, the theme builder, the MCP
server, the script, from C# and app tooling. Page recipes and common pages are not exempt: they are
whole pages to copy and goals answer with them, so each has an entry. Exempt a page only when no
goal could answer with it.

## The stamp

Each entry's `stamp` is a hash of everything that defines its page as documented: the page's own
`.razor`, every example it renders and every file under `Examples/<its file name>/`, the css part
that names each class the page's source names most, and every script part that names one of
those classes as a selector or one of the `data-*` attributes the page names. Line endings do not
count. `build/guide-stamp.py` says exactly what is read.

When a page, an example, or a part behind it changes, the stamp no longer matches and
`GuideStampTests` fails, naming the page, each source that changed and the command to run:

1. Read the page and its examples as they are now.
2. Correct the entry where it no longer holds — `useFor`, `combineWith`, `notFor`, `goals` — and the
   goals that answer with the page.
3. Run the command the failure names, for the pages you reviewed:

   ```bash
   build/guide-stamp.sh /table /table-large
   ```

`build/guide-stamp.sh --check` runs the same check, `--sources /table` lists what a page is stamped
against, and `--all` restamps everything — only after every entry has been read.

## What the tests enforce

| Test | Fails when |
|---|---|
| `DesignGuideTests.Every_guide_file_matches_the_schema` | a file has an unknown, missing or null key, or is not a group's name |
| `DesignGuideTests.Every_entry_says_what_it_is_for_and_what_goes_with_it` | `useFor` or a `how` is not a sentence, `combineWith` is empty, a `notFor` text ends with a full stop, or `stamp` is empty |
| `DesignGuideTests.Every_page_has_exactly_one_entry_unless_it_is_exempt` | a page of a written group has no entry or two, an entry is in another group's file or names no page, or an exempt page has one |
| `DesignGuideTests.Every_group_with_a_page_to_describe_has_its_file_or_is_pending` | a group has no file and is not pending, or `pending.json` names no group |
| `DesignGuideTests.Every_goal_is_complete` | an id is not kebab-case or not unique, the goal does not start `Let the user `, it has fewer than three phrases or a phrase another goal has, it has no single primary, or it is filed under a group its primary answer is not in |
| `DesignGuideTests.Goals_and_entries_name_each_other` | a goal answers with a page whose entry does not list it, or an entry lists a goal that does not exist or does not answer with it |
| `DesignGuideTests.Section_ids_goal_ids_and_topics_are_distinct` | a section id, a goal id, a page route and a `get_guide` topic share a name |
| `DesignGuideTests.The_principles_are_markdown_the_page_can_render` | `principles.md` uses Markdown the page does not render, or links a file that does not exist |
| `DesignGuideTests.The_app_embeds_the_guide_on_disk` | the catalogue's embedded copy differs from these files |
| `DesignGuideTests.Nothing_is_pending` | skipped until the last group is written; then `pending.json` must stay empty |
| `GuideReferenceTests` | a page or an anchor anywhere in the guide is not on the site |
| `GuideStampTests` | a stamp does not match its sources |
| `GuidePageTests` | a page does not show its entry, or the design guide page lacks a goal or a section |
| `McpGuideTests` | a phrase or a goal's sentence does not bring its goal back as the first goal and among the first three hits of `search`, or `get_guide` and `get_page` disagree with the files |
| `GuideSearchTests` (browser) | a phrase does not bring its goal back as the first goal in the topbar search, inside the rows its panel shows |

A search failure names the goal that won. Make the losing goal's phrase or sentence say what only
it is about, rather than weakening the other.

## Adding a goal

1. Put it in the file of the group its primary answer is in.
2. Write the sentence the way a reader would finish "I want the user to…", and the phrases a
   developer would type for it — including the names the same thing has elsewhere.
3. Answer with the page to start from as `primary`, pointed at the example to copy, and the pages
   it is built with as `part`.
4. Add the id to the `goals` of every page the answer names.
5. Run `dotnet test src/Sedna.UI.Catalogue.Tests --filter "FullyQualifiedName~Guide"`; the search
   tests need a browser for the topbar half.

## Writing a group

For the phase that fills the remaining groups, one group per change:

1. Read every page in the group and its examples, and `principles.md`.
2. Write `components/<group>.json`, one entry per page in the sidebar's order, with `"stamp": ""`.
3. Write `goals/<group>.json` for the goals whose primary answer is in the group; a group may have
   none, and then has no goals file.
4. Add your goal ids to the `goals` of the pages in other groups that your goals answer with, and
   add other groups' goals that answer with your pages to your entries — the failures name each
   one.
5. Run `build/guide-stamp.sh` with your group's routes.
6. Leave `pending.json` alone: a group whose file exists is checked in full whether or not it is
   listed. When every group is written, the lists are emptied and `Nothing_is_pending` loses its
   `Skip`.
7. Run the guide tests until they pass.
