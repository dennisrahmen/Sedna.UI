using System.Text.RegularExpressions;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// What a real ticket, host, address or company looks like — matched by form, never by name.
/// </summary>
/// <remarks>
/// <para>
/// The repository is public and the package's stylesheet and script are served by every app
/// that installs it, so a name lifted from a real system is published the moment it lands on
/// <c>main</c>. There used to be a deny-list of the actual names here, and that is the thing
/// this replaces: a literal list is itself a published list of the names it forbids, which is
/// the disclosure it exists to prevent. The <c>No real names</c> section of the repo's
/// <c>CLAUDE.md</c> is the rule; these are the parts of it a test can check.
/// </para>
/// <para>
/// A shape catches the whole class without naming a member of it. Three real public
/// authorities were in the examples and every one was recognisable by the word in front of
/// the place — so that word is what is matched, and no customer has to be named to do it.
/// </para>
/// <para>
/// Scan the text <b>with its comments intact</b>. A markup snippet in a comment is copied
/// verbatim into the shipped file, so it is exactly as published as a line of code — which is
/// how a first name used as demo content in <c>69-timeline.css</c> reached every installing
/// app while a comment-stripping guard watched.
/// </para>
/// </remarks>
internal static class RealWorldShapes
{
    private static readonly string[] Patterns =
    [
        // An ITSM record. The separator is optional because `INC-204471` sat in an example
        // for months while a pattern wanting the digits to touch the prefix looked straight
        // at it.
        @"\b(?:INC|CHG|REQ|SR|PRB|TASK)[-_ ]?\d{3,}",

        // A German public body, and a company's legal form. Both are the word in front of
        // or behind the name rather than the name.
        @"\b(?:Kreisverwaltung|Verbandsgemeinde|Landratsamt|Bezirksamt|Stadtwerke)\b",
        @"\b(?:GmbH|gGmbH|mbH|KGaA|OHG)\b",

        // An internal DNS label, including one hiding under a reserved domain:
        // `exch-02.corp.example` is RFC 2606 on the right and internal on the left.
        // `internal` before `intern`, or the shorter alternative matches first and the
        // word-boundary check then fails on the letter after it.
        @"\.(?:internal|intern|corp|lan)\b",

        // A private or link-local address. A real-looking internal address in a
        // copy-pasteable field reads as a real system's; the documentation ranges of
        // RFC 5737 are what belongs there.
        @"\b10\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
        @"\b192\.168\.\d{1,3}\.\d{1,3}\b",
        @"\b172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}\b",
    ];

    /// <summary>Every distinct match in <paramref name="text"/>, for a failure message.</summary>
    public static List<string> FoundIn(string text) =>
        Patterns
            .SelectMany(p => Regex.Matches(text, p, RegexOptions.IgnoreCase).Select(m => m.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToList();
}
