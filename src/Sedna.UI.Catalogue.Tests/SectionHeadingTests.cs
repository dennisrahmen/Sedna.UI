using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Guide;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The headings of every page read as one set: a short topic name in sentence case, never a
/// class name or a call, never "A dashboard". A long page groups its examples into topics, so
/// its contents read as a few topics with their examples nested under them.
/// </summary>
/// <remarks>
/// The class an example teaches belongs in the sentence under its heading, where it renders as
/// code; a heading that is the class says nothing to a reader who does not know it yet.
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
public partial class SectionHeadingTests
{
    /// <summary>More top-level sections than this and a page groups them.</summary>
    private const int MostTopLevel = 8;

    /// <summary>A heading is a name, not a sentence.</summary>
    private const int MostWords = 6;

    [GeneratedRegex(@"`|(?:^|\s)\.[a-z]|--[a-z]|<[a-z]|[A-Za-z]\(")]
    private static partial Regex Code();

    [GeneratedRegex(@"^(?:A|An|The)\s")]
    private static partial Regex Article();

    private static readonly PageContents Contents = new(DesignGuide.Embedded);

    [Fact]
    public void Every_heading_is_a_short_topic_name()
    {
        var problems = new List<string>();
        foreach (var page in CataloguePages.All)
        {
            foreach (var section in Contents.For(page.Route).SelectMany(s => s.Flatten()))
            {
                var title = section.Title;
                if (Code().IsMatch(title))
                    problems.Add($"{page.Route}: \"{title}\" names code — put the class in the sentence under it.");
                if (Article().IsMatch(title))
                    problems.Add($"{page.Route}: \"{title}\" opens with an article — name the topic.");
                if (title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MostWords)
                    problems.Add($"{page.Route}: \"{title}\" is longer than {MostWords} words.");
                if (!char.IsUpper(title[0]) && !char.IsDigit(title[0]))
                    problems.Add($"{page.Route}: \"{title}\" is not in sentence case.");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void A_long_page_groups_its_sections_and_every_group_holds_several()
    {
        var problems = new List<string>();
        foreach (var page in CataloguePages.All)
        {
            var sections = Contents.For(page.Route);
            if (sections.Count > MostTopLevel)
                problems.Add($"{page.Route} lists {sections.Count} sections at the top of its contents; group them with CatGroup.");

            foreach (var group in sections.Where(s => s.Children.Count == 1))
                problems.Add($"{page.Route}: the group \"{group.Title}\" holds one section; a group holds two or more.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_heading_on_a_page_has_its_own_id()
    {
        var problems = new List<string>();
        foreach (var page in CataloguePages.All)
        {
            foreach (var twice in Contents.For(page.Route).SelectMany(s => s.Flatten())
                         .GroupBy(s => s.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
                problems.Add($"{page.Route}: \"{twice.Key}\" is the id of {twice.Count()} headings.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
