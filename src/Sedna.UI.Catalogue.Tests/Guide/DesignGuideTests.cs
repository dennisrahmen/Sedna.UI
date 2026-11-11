using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Guide;
using Sedna.UI.Catalogue.Mcp;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The design guide's files hold to their schema, cover every page, and agree with each other.
/// </summary>
/// <remarks>
/// Read from disk, through the app's own parser, so a failure names the file to fix. The rules
/// are listed in <c>docs/design-guide/README.md</c>; every message here says which one broke
/// and what to write instead.
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
public partial class DesignGuideTests
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    [Fact]
    public void Every_guide_file_matches_the_schema()
    {
        var problems = new List<string>();

        foreach (var (file, json) in GuideFiles.Components)
        {
            try { DesignGuide.ParseEntries(file, json); }
            catch (InvalidDataException e) { problems.Add($"components/{e.Message}"); }
        }

        foreach (var (file, json) in GuideFiles.GoalFiles)
        {
            try { DesignGuide.ParseGoals(file, json); }
            catch (InvalidDataException e) { problems.Add($"goals/{e.Message}"); }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_entry_says_what_it_is_for_and_what_goes_with_it()
    {
        var problems = new List<string>();

        foreach (var entry in GuideFiles.Guide.Entries)
        {
            var at = $"{entry.Page} in components/{entry.Group.ToLowerInvariant()}.json";

            if (entry.Page.Contains('#', StringComparison.Ordinal))
                problems.Add($"{at}: \"page\" is a route with no anchor.");
            if (!IsSentence(entry.UseFor))
                problems.Add($"{at}: \"useFor\" is one sentence, ending with a full stop, saying what the reader does with it.");
            if (entry.CombineWith.Count == 0)
                problems.Add($"{at}: \"combineWith\" names at least one page, with how the two go together.");
            problems.AddRange(entry.CombineWith.Where(c => !IsSentence(c.How))
                .Select(c => $"{at}: the \"how\" for {c.Page} is a sentence ending with a full stop."));
            problems.AddRange(entry.NotFor.Where(n => n.Text.Trim().Length == 0 || n.Text.EndsWith('.'))
                .Select(n => $"{at}: a \"notFor\" text names the job, with no full stop: \"{n.Text}\"."));
            if (entry.Stamp.Length == 0)
                problems.Add($"{at}: \"stamp\" is empty. Run build/guide-stamp.sh {entry.Page}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_page_has_exactly_one_entry_unless_it_is_exempt()
    {
        var guide = GuideFiles.Guide;
        var problems = new List<string>();

        foreach (var page in CataloguePages.All)
        {
            var entries = guide.Entries.Where(e => e.Page == page.Route).ToList();
            var file = $"components/{page.Group.ToLowerInvariant()}.json";

            if (GuideFiles.Exempt.ContainsKey(page.Route))
            {
                if (entries.Count > 0)
                    problems.Add($"{page.Route} is exempt in GuideFiles.Exempt and has an entry. Drop one of the two.");
                continue;
            }

            if (GuideFiles.IsPending("components", page.Group)) continue;

            if (entries.Count == 0)
                problems.Add($"{page.Route} ({page.Label}) has no entry. Add one to {file}, or exempt it in "
                             + "GuideFiles.Exempt with the reason if it documents no component.");
            else if (entries.Count > 1)
                problems.Add($"{page.Route} has {entries.Count} entries; it takes one.");
            else if (entries[0].Group != page.Group)
                problems.Add($"{page.Route} is in the {page.Group} group, so its entry belongs in {file}, "
                             + $"not components/{entries[0].Group.ToLowerInvariant()}.json.");
        }

        foreach (var entry in guide.Entries.Where(e => CataloguePages.All.All(p => p.Route != e.Page)))
            problems.Add($"The entry for {entry.Page} in components/{entry.Group.ToLowerInvariant()}.json names no registered page.");

        foreach (var route in GuideFiles.Exempt.Keys.Where(r => CataloguePages.All.All(p => p.Route != r)))
            problems.Add($"GuideFiles.Exempt names {route}, which is no registered page.");

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_group_with_a_page_to_describe_has_its_file_or_is_pending()
    {
        var problems = new List<string>();
        foreach (var group in CataloguePages.Groups)
        {
            var needs = CataloguePages.InGroup(group).Any(p => !GuideFiles.Exempt.ContainsKey(p.Route));
            var path = Path.Combine(GuideFiles.Dir, "components", group.ToLowerInvariant() + ".json");
            if (needs && !File.Exists(path) && !GuideFiles.IsPending("components", group))
                problems.Add($"The {group} group has pages to describe and no components/{group.ToLowerInvariant()}.json.");
        }

        foreach (var kind in new[] { "components", "goals" })
            foreach (var group in GuideFiles.Pending(kind))
                if (!CataloguePages.Groups.Any(g => g.ToLowerInvariant() == group))
                    problems.Add($"pending.json lists \"{group}\" under {kind}, which is no catalogue group.");

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact(Skip = "Turned on when the last group has its entries and goals: then pending.json has to be "
                 + "empty, and this test is what keeps it so. docs/design-guide/README.md says when.")]
    public void Nothing_is_pending()
    {
        var pending = GuideFiles.Pending("components").Concat(GuideFiles.Pending("goals")).ToList();
        Assert.True(pending.Count == 0,
            $"docs/design-guide/pending.json still lists {string.Join(", ", pending)}. Write their files "
            + "and empty the lists.");
    }

    [Fact]
    public void Every_goal_is_complete()
    {
        var guide = GuideFiles.Guide;
        var problems = new List<string>();

        foreach (var goal in guide.Goals)
        {
            var at = $"Goal \"{goal.Id}\" in goals/{goal.Group.ToLowerInvariant()}.json";

            if (!KebabCase().IsMatch(goal.Id))
                problems.Add($"{at}: the id is kebab-case — lowercase words joined by hyphens.");
            if (!goal.Goal.StartsWith("Let the user ", StringComparison.Ordinal) || goal.Goal.EndsWith('.'))
                problems.Add($"{at}: \"goal\" starts \"Let the user \" and has no full stop.");
            if (goal.Phrases.Count < 3)
                problems.Add($"{at}: it has {goal.Phrases.Count} phrases; write at least three — what a developer "
                             + "would type, plain words and the names other libraries use.");
            problems.AddRange(goal.Phrases.Where(p => p.Trim().Length == 0 || p != p.ToLowerInvariant())
                .Select(p => $"{at}: the phrase \"{p}\" is lowercase words."));

            var primaries = goal.Answer.Count(a => a.Role == GuideAnswer.Primary);
            if (primaries != 1)
                problems.Add($"{at}: it has {primaries} answers with \"role\": \"primary\"; it takes exactly one.");
            problems.AddRange(goal.Answer.Where(a => a.Role is not (GuideAnswer.Primary or GuideAnswer.Part))
                .Select(a => $"{at}: \"role\" is \"primary\" or \"part\", not \"{a.Role}\"."));
            if (!IsSentence(goal.How)) problems.Add($"{at}: \"how\" is one to three sentences ending with a full stop.");
            if (!IsSentence(goal.Avoid)) problems.Add($"{at}: \"avoid\" is one sentence ending with a full stop.");

            var primaryGroup = GuideFiles.GroupOf(DesignGuide.Split(goal.PrimaryPage).Route);
            if (primaryGroup is not null && primaryGroup != goal.Group)
                problems.Add($"{at}: its primary answer {goal.PrimaryPage} is in the {primaryGroup} group, so the goal "
                             + $"belongs in goals/{primaryGroup.ToLowerInvariant()}.json.");
        }

        foreach (var duplicate in guide.Goals.GroupBy(g => g.Id).Where(g => g.Count() > 1))
            problems.Add($"Goal id \"{duplicate.Key}\" is used {duplicate.Count()} times; ids are unique across every file.");

        // A phrase is a name of its goal: search ranks a query that IS a phrase as that goal, so
        // two goals cannot share one.
        foreach (var shared in guide.Goals
                     .SelectMany(g => g.Phrases.Prepend(g.Id).Select(p => (Name: CatalogueRanker.Normalise(p), g.Id)))
                     .GroupBy(x => x.Name).Where(x => x.Select(y => y.Id).Distinct().Count() > 1))
            problems.Add($"\"{shared.Key}\" names more than one goal: {string.Join(", ", shared.Select(x => x.Id).Distinct())}.");

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Goals_and_entries_name_each_other()
    {
        var guide = GuideFiles.Guide;
        var problems = new List<string>();

        foreach (var goal in guide.Goals)
        {
            foreach (var route in goal.Answer.Select(a => DesignGuide.Split(a.Page).Route).Distinct())
            {
                var group = GuideFiles.GroupOf(route);
                if (group is null) continue; // GuideReferenceTests reports a page that does not exist.

                if (GuideFiles.Exempt.ContainsKey(route))
                {
                    problems.Add($"Goal \"{goal.Id}\" answers with {route}, which is exempt and has no entry. Answer "
                                 + "with a component page, or give that page an entry instead of the exemption.");
                    continue;
                }

                if (GuideFiles.IsPending("components", group)) continue;

                var entry = guide.EntryFor(route);
                if (entry is null) continue; // Every_page_has_exactly_one_entry reports it.
                if (!entry.Goals.Contains(goal.Id))
                    problems.Add($"Goal \"{goal.Id}\" answers with {route}, so add \"{goal.Id}\" to the \"goals\" of "
                                 + $"{route} in components/{entry.Group.ToLowerInvariant()}.json.");
            }
        }

        foreach (var entry in guide.Entries)
        {
            foreach (var id in entry.Goals)
            {
                var goal = guide.Goal(id);
                if (goal is null)
                    problems.Add($"{entry.Page} lists the goal \"{id}\", which no goals/*.json defines.");
                else if (goal.Answer.All(a => DesignGuide.Split(a.Page).Route != entry.Page))
                    problems.Add($"{entry.Page} lists the goal \"{id}\", whose \"answer\" does not name {entry.Page}. "
                                 + "Add the page to the goal's answer as a part, or drop the goal from the entry.");
            }

            foreach (var duplicate in entry.Goals.GroupBy(g => g).Where(g => g.Count() > 1))
                problems.Add($"{entry.Page} lists the goal \"{duplicate.Key}\" twice.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Section_ids_goal_ids_and_topics_are_distinct()
    {
        // get_guide answers a section id, a goal id, "principles", "goals" or a route, and the
        // design guide page gives each section and goal its id: one name has to mean one thing.
        var guide = GuideFiles.Guide;
        var names = guide.Sections.Select(s => (s.Id, $"section \"{s.Title}\""))
            .Concat(guide.Goals.Select(g => (g.Id, $"goal \"{g.Id}\"")))
            .Concat(CataloguePages.All.Select(p => (p.Route.TrimStart('/'), $"page {p.Route}")))
            .Concat([("principles", "the topic \"principles\""), ("goals", "the topic \"goals\"")])
            .Concat(CataloguePages.Groups.Select(g => ("goals-" + g.ToLowerInvariant(), $"the {g} goals heading")));

        var clashes = names.GroupBy(n => n.Item1).Where(g => g.Count() > 1)
            .Select(g => $"\"{g.Key}\" is {string.Join(" and ", g.Select(n => n.Item2))}.").ToList();

        Assert.True(clashes.Count == 0, string.Join(Environment.NewLine, clashes));
    }

    [Fact]
    public void The_principles_cover_what_an_agent_is_told_they_cover()
    {
        // McpInstructions, get_guide's description and the README name these sections.
        var ids = GuideFiles.Guide.Sections.Select(s => s.Id).ToList();
        foreach (var id in new[] { "page-composition", "spacing", "colour", "actions", "feedback", "density", "motion", "words" })
            Assert.True(ids.Contains(id), $"principles.md has no \"## \" section whose id is \"{id}\". Sections: {string.Join(", ", ids)}");
    }

    [Fact]
    public void The_principles_are_markdown_the_page_can_render()
    {
        var guide = GuideFiles.Guide;
        var problems = new List<string>();
        foreach (var section in guide.Sections)
        {
            try { GuideMarkdown.ToHtml(section.Markdown); }
            catch (FormatException e) { problems.Add($"## {section.Title}: {e.Message}"); }
        }

        // A document beside it is a relative path that exists, so the link works on GitHub too.
        foreach (var link in GuideMarkdown.Links(GuideFiles.Principles).Where(l => !l.StartsWith("http", StringComparison.Ordinal)))
        {
            var path = Path.GetFullPath(Path.Combine(GuideFiles.Dir, link.Split('#')[0]));
            if (!File.Exists(path)) problems.Add($"principles.md links to {link}, which is no file.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_app_embeds_the_guide_on_disk()
    {
        // The csproj globs docs/design-guide/ and the Dockerfile copies docs/: a file one of them
        // missed would be on GitHub and absent from the site and the MCP server.
        var embedded = DesignGuide.EmbeddedFiles();
        var onDisk = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["principles.md"] = GuideFiles.Principles };
        foreach (var (file, json) in GuideFiles.Components) onDisk["components/" + file] = json;
        foreach (var (file, json) in GuideFiles.GoalFiles) onDisk["goals/" + file] = json;

        Assert.Equal(onDisk.Keys, embedded.Keys);
        foreach (var (path, text) in onDisk)
            Assert.True(text == embedded[path], $"The embedded {path} differs from the file on disk. Rebuild the catalogue.");
    }

    private static bool IsSentence(string text) =>
        text.Trim().Length > 0 && text.TrimEnd().EndsWith('.');
}
