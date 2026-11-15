using System.Text;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// The trait that marks a scan of every catalogue page, which CI splits across jobs with
/// <c>SEDNA_UI_SHARD</c>.
/// </summary>
public static class ScanKind
{
    /// <summary>The trait's name.</summary>
    public const string Name = "Kind";

    /// <summary>The value for a class that walks every page.</summary>
    public const string Scan = "Scan";
}

/// <summary>
/// Which share of the catalogue's routes this run scans, chosen by <c>SEDNA_UI_SHARD=k/n</c>.
/// </summary>
/// <remarks>
/// <para>
/// A route belongs to the shard its own text says: an FNV-1a hash of its UTF-8 bytes,
/// finished with a bit mixer, modulo <c>n</c>. Not <c>string.GetHashCode</c>, which differs in
/// every process, and not a route's position in the list, which moves every later route when
/// a page is added. Adding a page changes one route's shard and no other.
/// </para>
/// <para>
/// Unset means every route. A malformed value, a <c>k</c> outside <c>1..n</c> or an <c>n</c>
/// below 1 throws, as an unknown <c>SEDNA_UI_BROWSER</c> does, so a typo cannot run nothing
/// and report green.
/// </para>
/// <para>
/// A shard that holds no route — only possible when <c>n</c> exceeds the number of routes — is
/// legal and runs nothing: <c>ShardTests</c> proves the shards of any <c>n</c> are disjoint and
/// together exactly the catalogue, so an empty one lost no page.
/// </para>
/// </remarks>
internal readonly record struct Shard(int K, int N)
{
    public const string EnvVar = "SEDNA_UI_SHARD";

    /// <summary>The whole catalogue: one shard of one.</summary>
    public static Shard Whole => new(1, 1);

    /// <summary>The shard this run was asked for.</summary>
    public static Shard Current => Parse(Environment.GetEnvironmentVariable(EnvVar));

    public static Shard Parse(string? value)
    {
        if (string.IsNullOrEmpty(value)) return Whole;
        var parts = value.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var k)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            || n < 1 || k < 1 || k > n)
            throw new InvalidOperationException(
                $"{EnvVar}={value} names no shard. Use k/n with 1 <= k <= n, for example 1/4.");
        return new(k, n);
    }

    /// <summary>The shard, 1-based, that <paramref name="route"/> belongs to among <paramref name="n"/>.</summary>
    public static int Of(string route, int n)
    {
        var h = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(route)) h = (h ^ b) * 16777619u;
        // FNV-1a's low bits are weak for a short string: finish it with murmur3's mixer.
        h ^= h >> 16; h *= 0x85ebca6bu; h ^= h >> 13; h *= 0xc2b2ae35u; h ^= h >> 16;
        return (int)(h % (uint)n) + 1;
    }

    public bool Holds(string route) => Of(route, N) == K;

    public IReadOnlyList<string> Select(IEnumerable<string> routes) => routes.Where(Holds).ToList();
}
