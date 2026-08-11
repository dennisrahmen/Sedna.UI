namespace Sedna.UI.Catalogue.Mcp;

/// <summary>What a hit matched on, so an agent can discard it without fetching it.</summary>
internal sealed record Hit(
    string Kind, string Ref, string Title, string Blurb, string Page,
    IReadOnlyList<string> Classes, int Score, string MatchedOn);

/// <summary>
/// Ranks the catalogue against a query.
/// </summary>
/// <remarks>
/// <para>
/// The tiering is the command palette's (<c>js-parts/24-palette.js</c>); the fields
/// it runs on are not. The palette scores one short label, and its subsequence
/// fallback — <c>400 - (span - n) - first</c> — is dominated by noise the moment it
/// meets a kilobyte of markup, where almost every query matches almost everything.
/// </para>
/// <para>
/// So: <b>subsequence never runs on a long field.</b> That is the single most
/// important line here. Markup and blurbs are matched by whole word only.
/// </para>
/// <para>
/// A multi-word query is AND over its terms, scored as the mean of each term's best
/// tier, so a two-word and a five-word query are comparable. The sort is stable and
/// tie-broken by source order, so two identical calls return identical JSON.
/// </para>
/// <para>
/// Linear over roughly a hundred examples and two hundred classes. This is the
/// correct amount of machinery, and the reason to take no search dependency.
/// </para>
/// </remarks>
internal static class CatalogueRanker
{
    private const int Exact = 1000;
    private const int Prefix = 900;
    private const int WordStart = 800;
    private const int Substring = 600;
    private const int Subsequence = 400;
    private const int LongFieldWord = 200;
    private const int MarkupWord = 100;

    /// <summary>Normalises both sides so `btn-go`, `.btn-go`, `btn go` and `BtnGo` agree.</summary>
    public static string Normalise(string text)
    {
        var lower = text.ToLowerInvariant().TrimStart('.');
        if (lower.StartsWith("--", StringComparison.Ordinal)) lower = lower[2..];
        return lower.Replace('_', ' ').Replace('-', ' ');
    }

    public static IEnumerable<string> Terms(string query) =>
        Normalise(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Scores one candidate. Returns null when any term fails to match anything.
    /// </summary>
    /// <param name="terms">The normalised query terms.</param>
    /// <param name="identifier">An exact-matchable name — a class, an id, a route.</param>
    /// <param name="shortFields">Title-length text. Subsequence runs here and nowhere else.</param>
    /// <param name="longFields">Blurbs and markup. Whole-word matching only.</param>
    public static (int Score, string MatchedOn)? Score(
        IReadOnlyList<string> terms,
        string identifier,
        IEnumerable<string> shortFields,
        IEnumerable<string> longFields)
    {
        if (terms.Count == 0) return (1, "everything");

        var id = Normalise(identifier);

        // The whole query IS the identifier. Without this, "badge go" loses
        // `.badge-go` to `.badge`: the shorter name scores an exact match on the
        // first term and finds the second among its own modifiers, and one exact
        // hit outweighs two near-exact ones. Scoring the query as a unit first is
        // what makes a compound name findable by its compound name.
        if (id == string.Join(' ', terms)) return (Exact + 200, "name");
        var shorts = shortFields.Select(Normalise).ToList();
        var longs = longFields.Select(Normalise).ToList();

        var total = 0;
        string? best = null;
        var bestTier = -1;

        foreach (var term in terms)
        {
            var (tier, where) = BestFor(term, id, shorts, longs);
            if (tier < 0) return null;              // AND: every term has to land

            total += tier;
            if (tier > bestTier) (bestTier, best) = (tier, where);
        }

        return (total / terms.Count, best ?? "name");
    }

    private static (int Tier, string Where) BestFor(
        string term, string identifier, List<string> shorts, List<string> longs)
    {
        if (identifier == term) return (Exact, "name");
        if (identifier.StartsWith(term, StringComparison.Ordinal)) return (Prefix, "name");

        var tier = -1;
        var where = "name";

        foreach (var field in shorts)
        {
            var at = field.IndexOf(term, StringComparison.Ordinal);
            if (at < 0) continue;

            var score = at == 0 ? Prefix
                : field[at - 1] == ' ' ? WordStart
                : Substring;
            if (score > tier) (tier, where) = (score, "title");
        }

        if (tier < 0 && IsWord(identifier, term)) (tier, where) = (WordStart, "name");

        // Subsequence, short fields only, penalised by how spread out it is.
        if (tier < 0)
        {
            foreach (var field in shorts.Append(identifier))
            {
                var span = SubsequenceSpan(term, field);
                if (span < 0) continue;

                var score = Subsequence - (span - term.Length);
                if (score > tier) (tier, where) = (score, "title");
            }
        }

        if (tier < 0)
        {
            foreach (var field in longs)
            {
                if (!IsWord(field, term)) continue;

                var score = field.Length < 400 ? LongFieldWord : MarkupWord;
                if (score > tier) (tier, where) = (score, field.Length < 400 ? "blurb" : "markup");
            }
        }

        return (tier, where);
    }

    private static bool IsWord(string haystack, string term)
    {
        for (var at = haystack.IndexOf(term, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(term, at + 1, StringComparison.Ordinal))
        {
            var before = at == 0 || haystack[at - 1] == ' ';
            var afterAt = at + term.Length;
            var after = afterAt == haystack.Length || haystack[afterAt] == ' ';
            if (before && after) return true;
        }

        return false;
    }

    /// <summary>The span the term occupies as a subsequence, or -1 if absent.</summary>
    private static int SubsequenceSpan(string term, string haystack)
    {
        int first = -1, last = -1, from = 0;

        foreach (var c in term)
        {
            var at = haystack.IndexOf(c, from);
            if (at < 0) return -1;
            if (first < 0) first = at;
            last = at;
            from = at + 1;
        }

        return last - first + 1;
    }
}
