using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Navigation;

namespace Sedna.UI.Catalogue.Guide;

/// <summary>
/// The Markdown <c>principles.md</c> is written in, and nothing more: paragraphs, <c>- </c>
/// lists, tables, <c>### </c> headings, and inline code, bold and links.
/// </summary>
/// <remarks>
/// <para>
/// No package: the catalogue takes none it can avoid, and the guide needs a small, fixed subset.
/// Anything outside it — a fence, a quote, a numbered list, raw HTML — throws, so
/// <c>DesignGuideTests</c> fails on it instead of the page printing it as text.
/// </para>
/// <para>
/// Links are written so they work where the file is read on GitHub: a catalogue page as its
/// address on the public site, a document beside it as a relative path. <see cref="Link"/> turns
/// them into what each reader needs — a path on this site for the web page, an absolute address
/// for an agent.
/// </para>
/// </remarks>
internal static partial class GuideMarkdown
{
    [GeneratedRegex(@"`([^`]+)`|\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex Inline();

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex LinkTarget();

    private const string RepoDocs = CatalogueLinks.Repo + "/blob/main/docs/";

    /// <summary>Every link target in the text, as written.</summary>
    public static IEnumerable<string> Links(string markdown) =>
        LinkTarget().Matches(markdown).Select(m => m.Groups[1].Value);

    /// <summary>
    /// Where a link written in <c>docs/design-guide/</c> goes: a site route for a catalogue
    /// page when <paramref name="forSite"/>, else an absolute address.
    /// </summary>
    public static string Link(string target, bool forSite)
    {
        var site = CatalogueLinks.Site.TrimEnd('/');
        if (target.StartsWith(site + "/", StringComparison.Ordinal))
            return forSite ? target[site.Length..] : target;
        if (target.StartsWith("../", StringComparison.Ordinal))
            return RepoDocs + target[3..];
        if (target.StartsWith("http", StringComparison.Ordinal))
            return target;
        return RepoDocs + "design-guide/" + target;
    }

    /// <summary>The Markdown with every link made absolute, for a reader that is not on the site.</summary>
    public static string Absolute(string markdown) =>
        LinkTarget().Replace(markdown, m => $"]({Link(m.Groups[1].Value, forSite: false)})");

    /// <summary>The Markdown as HTML for the design guide page.</summary>
    public static string ToHtml(string markdown)
    {
        var html = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var i = 0; i < lines.Length;)
        {
            var line = lines[i];
            if (line.Trim().Length == 0) { i++; continue; }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                html.Append("<h3>").Append(InlineHtml(line[4..].Trim())).Append("</h3>\n");
                i++;
            }
            else if (line.StartsWith('|'))
            {
                var rows = new List<string>();
                while (i < lines.Length && lines[i].StartsWith('|')) rows.Add(lines[i++]);
                Table(rows, html);
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                html.Append("<ul>\n");
                while (i < lines.Length && lines[i].StartsWith("- ", StringComparison.Ordinal))
                {
                    var item = new List<string> { lines[i++][2..].Trim() };
                    while (i < lines.Length && lines[i].StartsWith("  ", StringComparison.Ordinal) && lines[i].Trim().Length > 0)
                        item.Add(lines[i++].Trim());
                    html.Append("<li>").Append(InlineHtml(string.Join(' ', item))).Append("</li>\n");
                }
                html.Append("</ul>\n");
            }
            else
            {
                Unsupported(line);
                var paragraph = new List<string>();
                while (i < lines.Length && lines[i].Trim().Length > 0
                       && !lines[i].StartsWith('|') && !lines[i].StartsWith("- ", StringComparison.Ordinal)
                       && !lines[i].StartsWith('#'))
                {
                    Unsupported(lines[i]);
                    paragraph.Add(lines[i++].Trim());
                }
                html.Append("<p>").Append(InlineHtml(string.Join(' ', paragraph))).Append("</p>\n");
            }
        }

        return html.ToString();
    }

    private static void Unsupported(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#') || trimmed.StartsWith('>') || trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith('<') || Regex.IsMatch(trimmed, @"^\d+\. ") || trimmed.StartsWith("* ", StringComparison.Ordinal))
            throw new FormatException(
                $"The design guide's Markdown holds paragraphs, \"- \" lists, tables, \"### \" headings, `code`, "
                + $"**bold** and [links](...) and nothing else. Rewrite this line: {line}");
    }

    private static void Table(List<string> rows, StringBuilder html)
    {
        static string[] Cells(string row) =>
            row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

        if (rows.Count < 2 || !Regex.IsMatch(rows[1], @"^\|(\s*:?-+:?\s*\|)+\s*$"))
            throw new FormatException($"A table needs a header row and a |---| row under it: {rows[0]}");

        html.Append("<table>\n<thead><tr>");
        foreach (var cell in Cells(rows[0])) html.Append("<th>").Append(InlineHtml(cell)).Append("</th>");
        html.Append("</tr></thead>\n<tbody>\n");
        foreach (var row in rows.Skip(2))
        {
            html.Append("<tr>");
            foreach (var cell in Cells(row)) html.Append("<td>").Append(InlineHtml(cell)).Append("</td>");
            html.Append("</tr>\n");
        }
        html.Append("</tbody>\n</table>\n");
    }

    /// <summary>One line of inline Markdown as HTML, everything else escaped.</summary>
    public static string InlineHtml(string text)
    {
        var html = new StringBuilder();
        var at = 0;
        foreach (Match m in Inline().Matches(text))
        {
            html.Append(WebUtility.HtmlEncode(text[at..m.Index]));
            if (m.Groups[1].Success)
                html.Append("<code>").Append(WebUtility.HtmlEncode(m.Groups[1].Value)).Append("</code>");
            else if (m.Groups[2].Success)
                html.Append("<strong>").Append(InlineHtml(m.Groups[2].Value)).Append("</strong>");
            else
                html.Append("<a href=\"").Append(WebUtility.HtmlEncode(Link(m.Groups[4].Value, forSite: true)))
                    .Append("\">").Append(InlineHtml(m.Groups[3].Value)).Append("</a>");
            at = m.Index + m.Length;
        }
        html.Append(WebUtility.HtmlEncode(text[at..]));
        return html.ToString();
    }
}
