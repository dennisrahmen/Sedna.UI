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
/// is a DevTools domain, which Playwright's WebKit does not offer. A page that was closed before
/// <see cref="DrainAsync"/> is not collected.
/// </para>
/// <para>
/// A test base calls <see cref="AttachAsync"/> right after it creates a page, before the page
/// navigates, and <see cref="DrainAsync"/> once before it disposes the browser.
/// </para>
/// </remarks>
internal static class JsCoverage
{
    private const string DirVariable = "SEDNA_UI_JS_COVERAGE_DIR";

    private static readonly ConcurrentQueue<ICDPSession> Sessions = new();
    private static readonly ConcurrentDictionary<string, byte[]> Covered = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static bool Enabled =>
        !BrowserEngine.IsWebKit && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DirVariable));

    public static async Task AttachAsync(IBrowserContext context, IPage page)
    {
        if (!Enabled) return;
        var session = await context.NewCDPSessionAsync(page);
        await session.SendAsync("Profiler.enable");
        await session.SendAsync("Profiler.startPreciseCoverage", new() { ["callCount"] = false, ["detailed"] = true });
        Sessions.Enqueue(session);
    }

    public static async Task DrainAsync()
    {
        if (!Enabled) return;
        while (Sessions.TryDequeue(out var session))
        {
            try
            {
                var taken = await session.SendAsync("Profiler.takePreciseCoverage");
                Merge(taken!.Value);
            }
            catch (PlaywrightException)
            {
                // The page was closed by its test: its coverage went with it.
            }
        }

        Write();
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

            var length = File.ReadAllText(file).Length;
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

    private static void Write()
    {
        var directory = Environment.GetEnvironmentVariable(DirVariable)!;
        Directory.CreateDirectory(directory);
        var scripts = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (path, covered) in Covered)
        {
            var runs = new List<int[]>();
            for (var i = 0; i < covered.Length; i++)
            {
                if (covered[i] == 0) continue;
                var start = i;
                while (i < covered.Length && covered[i] == 1) i++;
                runs.Add([start, i]);
            }

            var text = File.ReadAllText(Assets.WwwrootFile(path)!);
            scripts[path] = new
            {
                length = text.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant(),
                covered = runs,
            };
        }

        lock (Gate)
        {
            var file = Path.Combine(directory, $"js-{Environment.ProcessId}.json");
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(new { scripts }));
            File.Move(file + ".tmp", file, overwrite: true);
        }
    }
}
