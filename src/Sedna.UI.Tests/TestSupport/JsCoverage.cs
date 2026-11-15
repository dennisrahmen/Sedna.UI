using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// V8 block coverage of the shipped scripts (<c>/js/Sedna.UI*.js</c>), collected through the
/// Chromium DevTools Profiler from the pages the browser tests open, merged across every page
/// of the process and written as one file per process. <c>build/coverage-js.py</c> maps the
/// offsets back to <c>js-parts/</c> lines.
/// </summary>
/// <remarks>
/// <para>
/// Off unless <c>SEDNA_UI_JS_COVERAGE_DIR</c> names a directory, and Chromium only: the Profiler
/// is a DevTools domain, which Playwright's WebKit does not offer. With the variable unset
/// nothing here opens a session or reads a file.
/// </para>
/// <para>
/// <see cref="BrowserTestBase"/> calls <see cref="AttachAsync"/> right after it creates a page,
/// before the page navigates, and <see cref="DrainAsync"/> with that test's sessions before it
/// closes the test's contexts. The browser is shared by the whole process, so a page's coverage
/// is taken when its own test ends and not once for the browser; a page closed first is lost.
/// The merged result is written when the process exits, and at most every few seconds before
/// that, so a test host that is killed leaves a recent file behind.
/// </para>
/// </remarks>
internal static class JsCoverage
{
    private const string DirVariable = "SEDNA_UI_JS_COVERAGE_DIR";
    private static readonly TimeSpan WriteEvery = TimeSpan.FromSeconds(5);

    private static readonly ConcurrentDictionary<string, byte[]> Covered = new(StringComparer.Ordinal);
    // What a served script is — its text length and hash — read once per process, not per page.
    private static readonly ConcurrentDictionary<string, (int Length, string Sha256)> Served = new(StringComparer.Ordinal);
    private static readonly object Gate = new();
    private static DateTime _lastWrite = DateTime.MinValue;
    private static bool _exitHooked;

    public static bool Enabled =>
        !BrowserEngine.IsWebKit && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DirVariable));

    /// <summary>Starts collecting from <paramref name="page"/>; null when collection is off.</summary>
    public static async Task<ICDPSession?> AttachAsync(IBrowserContext context, IPage page)
    {
        if (!Enabled) return null;
        lock (Gate)
        {
            if (!_exitHooked)
            {
                _exitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Write(force: true);
            }
        }

        var session = await context.NewCDPSessionAsync(page);
        await session.SendAsync("Profiler.enable");
        await session.SendAsync("Profiler.startPreciseCoverage", new() { ["callCount"] = false, ["detailed"] = true });
        return session;
    }

    /// <summary>Takes what these sessions' pages covered and merges it in. Before their context closes.</summary>
    public static async Task DrainAsync(IEnumerable<ICDPSession> sessions)
    {
        if (!Enabled) return;
        foreach (var session in sessions)
        {
            try
            {
                var taken = await session.SendAsync("Profiler.takePreciseCoverage");
                Merge(taken!.Value);
            }
            catch (PlaywrightException)
            {
                // The page or its browser had already gone: its coverage went with it.
            }
        }

        Write(force: false);
    }

    private static void Merge(JsonElement taken)
    {
        foreach (var script in taken.GetProperty("result").EnumerateArray())
        {
            var url = script.GetProperty("url").GetString() ?? "";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
            var path = uri.AbsolutePath;
            if (!path.StartsWith("/js/Sedna.UI", StringComparison.Ordinal) || Assets.WwwrootFile(path) is not { } file)
                continue;

            var length = Described(path, file).Length;
            var ranges = new List<(int Start, int End, int Count)>();
            foreach (var function in script.GetProperty("functions").EnumerateArray())
                foreach (var r in function.GetProperty("ranges").EnumerateArray())
                    ranges.Add((r.GetProperty("startOffset").GetInt32(), r.GetProperty("endOffset").GetInt32(), r.GetProperty("count").GetInt32()));

            // Parents before the ranges they contain, so the innermost range paints last.
            ranges.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));
            var page = new byte[length];
            foreach (var (start, end, count) in ranges)
                page.AsSpan(start, Math.Min(end, length) - start).Fill(count > 0 ? (byte)1 : (byte)0);

            var merged = Covered.GetOrAdd(path, _ => new byte[length]);
            lock (merged)
                for (var i = 0; i < length; i++)
                    merged[i] |= page[i];
        }
    }

    private static (int Length, string Sha256) Described(string path, string file) =>
        Served.GetOrAdd(path, _ =>
        {
            var text = File.ReadAllText(file);
            return (text.Length, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
        });

    private static void Write(bool force)
    {
        lock (Gate)
        {
            if (!force && DateTime.UtcNow - _lastWrite < WriteEvery) return;
            _lastWrite = DateTime.UtcNow;

            var directory = Environment.GetEnvironmentVariable(DirVariable)!;
            Directory.CreateDirectory(directory);
            var scripts = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var (path, covered) in Covered)
            {
                var runs = new List<int[]>();
                lock (covered)
                {
                    for (var i = 0; i < covered.Length; i++)
                    {
                        if (covered[i] == 0) continue;
                        var start = i;
                        while (i < covered.Length && covered[i] == 1) i++;
                        runs.Add([start, i]);
                    }
                }

                var served = Served[path];
                scripts[path] = new { length = served.Length, sha256 = served.Sha256, covered = runs };
            }

            var file = Path.Combine(directory, $"js-{Environment.ProcessId}.json");
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(new { scripts }));
            File.Move(file + ".tmp", file, overwrite: true);
        }
    }
}
