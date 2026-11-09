using System.Text;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// Reads a QR code's module matrix back to its text, the way a scanner would, and checks every
/// part of the symbol on the way: the format information and its BCH code, the version
/// information from version 7, the Reed–Solomon code of every block, the byte-mode header, the
/// terminator and the pad codewords, and that the mask chosen scores lowest of the eight.
/// </summary>
/// <remarks>
/// Written from ISO/IEC 18004 for the tests, apart from the library's encoder, so a fault in
/// one is not mirrored in the other. Byte mode only, which is all the encoder writes. Every
/// failure throws <see cref="InvalidDataException"/> naming what was wrong.
/// </remarks>
internal static class QrDecoder
{
    internal sealed record Result(string Text, int Version, char Level, int Mask);

    /// <summary>Error correction codewords per block, by level (L, M, Q, H) and version.</summary>
    private static readonly int[][] EccPerBlock =
    [
        [0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [0, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [0, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    /// <summary>Blocks, by level (L, M, Q, H) and version.</summary>
    private static readonly int[][] Blocks =
    [
        [0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [0, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [0, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    private const string LevelNames = "LMQH";

    /// <summary>The level the two format bits name: 01 L, 00 M, 11 Q, 10 H.</summary>
    private static int LevelOfBits(int bits) => bits switch { 1 => 0, 0 => 1, 3 => 2, _ => 3 };

    private static int BitsOfLevel(int level) => level switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 };

    public static Result Decode(bool[,] m)
    {
        var n = m.GetLength(0);
        if (n != m.GetLength(1) || (n - 17) % 4 != 0 || n < 21 || n > 177)
            throw new InvalidDataException($"{n}×{m.GetLength(1)} is no QR code size");
        var version = (n - 17) / 4;

        // Format information, both copies, each checked against its BCH code.
        var first = 0;
        for (var i = 0; i < 15; i++) first |= (m[FormatA(i).Y, FormatA(i).X] ? 1 : 0) << i;
        var second = 0;
        for (var i = 0; i < 15; i++) second |= (m[FormatB(i, n).Y, FormatB(i, n).X] ? 1 : 0) << i;
        if (first != second) throw new InvalidDataException("The two copies of the format information differ");
        var format = first ^ 0x5412;
        if (FormatBits(format >> 10) != first) throw new InvalidDataException("The format information fails its BCH check");
        var level = LevelOfBits(format >> 13);
        var mask = (format >> 10) & 7;
        if (!m[n - 8, 8]) throw new InvalidDataException("The dark module is light");

        if (version >= 7)
        {
            var a = 0;
            var b = 0;
            for (var i = 0; i < 18; i++)
            {
                a |= (m[i / 3, n - 11 + (i % 3)] ? 1 : 0) << i;
                b |= (m[n - 11 + (i % 3), i / 3] ? 1 : 0) << i;
            }
            if (a != b || a != VersionBits(version)) throw new InvalidDataException($"The version information does not say {version}");
        }

        var function = FunctionModules(version);
        CheckFunctionPatterns(m, version);

        // The data modules, unmasked, in placement order.
        var bits = new List<bool>();
        foreach (var (x, y) in DataOrder(n, function)) bits.Add(m[y, x] ^ Masked(mask, x, y));

        var raw = bits.Count / 8;
        var codewords = new byte[raw];
        for (var i = 0; i < raw; i++)
        {
            var v = 0;
            for (var j = 0; j < 8; j++) v = (v << 1) | (bits[(i * 8) + j] ? 1 : 0);
            codewords[i] = (byte)v;
        }

        var data = Deinterleave(codewords, version, level);
        var text = ReadByteMode(data, version);

        // The mask: the lowest penalty of the eight, the first of equals.
        var unmasked = (bool[,])m.Clone();
        foreach (var (x, y) in DataOrder(n, function)) unmasked[y, x] ^= Masked(mask, x, y);
        var scores = Enumerable.Range(0, 8).Select(k => Penalty(Remasked(unmasked, function, level, k))).ToArray();
        var best = Array.IndexOf(scores, scores.Min());
        if (best != mask)
            throw new InvalidDataException($"Mask {mask} scores {scores[mask]}; mask {best} scores {scores[best]}");

        return new Result(text, version, LevelNames[level], mask);
    }

    private static (int X, int Y) FormatA(int i) => i switch
    {
        <= 5 => (8, i),
        6 => (8, 7),
        7 => (8, 8),
        8 => (7, 8),
        _ => (14 - i, 8),
    };

    private static (int X, int Y) FormatB(int i, int n) => i < 8 ? (n - 1 - i, 8) : (8, n - 15 + i);

    private static int FormatBits(int data)
    {
        var rem = data;
        for (var i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        return ((data << 10) | (rem & 0x3ff)) ^ 0x5412;
    }

    private static int VersionBits(int version)
    {
        var rem = version;
        for (var i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1f25);
        return (version << 12) | (rem & 0xfff);
    }

    private static int[] AlignmentCentres(int version)
    {
        if (version == 1) return [];
        var count = (version / 7) + 2;
        var step = (((version * 8) + (count * 3) + 5) / ((count * 4) - 4)) * 2;
        var list = new List<int> { 6 };
        for (var pos = (version * 4) + 17 - 7; list.Count < count; pos -= step) list.Insert(1, pos);
        return list.ToArray();
    }

    private static bool[,] FunctionModules(int version)
    {
        var n = (version * 4) + 17;
        var f = new bool[n, n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                if (x == 6 || y == 6) f[y, x] = true;
                if (x < 9 && y < 9) f[y, x] = true;
                if (x >= n - 8 && y < 9) f[y, x] = true;
                if (x < 9 && y >= n - 8) f[y, x] = true;
                if (version >= 7 && (InVersionBlock(x, y, n) || InVersionBlock(y, x, n))) f[y, x] = true;
            }
        }
        var centres = AlignmentCentres(version);
        var last = centres.Length - 1;
        for (var i = 0; i <= last; i++)
        {
            for (var j = 0; j <= last; j++)
            {
                // None over the three finders.
                if (OverFinder(i, j, last)) continue;
                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++) f[centres[i] + dy, centres[j] + dx] = true;
            }
        }
        return f;
    }

    /// <summary>The version block beside the top-right finder: three columns wide, six rows tall.
    /// Called with x and y swapped it is the block below the bottom-left finder.</summary>
    private static bool InVersionBlock(int x, int y, int n) => x >= n - 11 && x < n - 8 && y < 6;

    /// <summary>Whether the alignment centre at row <paramref name="i"/>, column <paramref name="j"/>
    /// would sit on one of the three finders, where no alignment pattern is drawn.</summary>
    private static bool OverFinder(int i, int j, int last) => i == 0 ? j == 0 || j == last : i == last && j == 0;

    private static void CheckFunctionPatterns(bool[,] m, int version)
    {
        var n = m.GetLength(0);
        foreach (var (cx, cy) in new[] { (3, 3), (n - 4, 3), (3, n - 4) })
        {
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    var ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (m[y, x] != (ring != 2 && ring != 4)) throw new InvalidDataException($"The finder at {cx},{cy} is wrong at {x},{y}");
                }
            }
        }
        for (var i = 8; i < n - 8; i++)
        {
            if (m[6, i] != (i % 2 == 0) || m[i, 6] != (i % 2 == 0)) throw new InvalidDataException($"The timing pattern is wrong at {i}");
        }
        var centres = AlignmentCentres(version);
        var last = centres.Length - 1;
        for (var i = 0; i <= last; i++)
        {
            for (var j = 0; j <= last; j++)
            {
                if (OverFinder(i, j, last)) continue;
                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                        if (m[centres[i] + dy, centres[j] + dx] != (Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1))
                            throw new InvalidDataException($"The alignment pattern at {centres[j]},{centres[i]} is wrong");
            }
        }
    }

    private static IEnumerable<(int X, int Y)> DataOrder(int n, bool[,] function)
    {
        for (var right = n - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            var upward = ((right + 1) & 2) == 0;
            for (var step = 0; step < n; step++)
            {
                var y = upward ? n - 1 - step : step;
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    if (!function[y, x]) yield return (x, y);
                }
            }
        }
    }

    private static bool Masked(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => ((x / 3) + (y / 2)) % 2 == 0,
        5 => ((x * y) % 2) + ((x * y) % 3) == 0,
        6 => (((x * y) % 2) + ((x * y) % 3)) % 2 == 0,
        _ => (((x + y) % 2) + ((x * y) % 3)) % 2 == 0,
    };

    private static byte[] Deinterleave(byte[] codewords, int version, int level)
    {
        var blockCount = Blocks[level][version];
        var ecc = EccPerBlock[level][version];
        var shortBlocks = blockCount - (codewords.Length % blockCount);
        var shortLength = codewords.Length / blockCount;
        var blocks = Enumerable.Range(0, blockCount)
            .Select(i => new List<byte>(shortLength + 1)).ToArray();
        var dataLength = Enumerable.Range(0, blockCount).Select(i => shortLength - ecc + (i < shortBlocks ? 0 : 1)).ToArray();

        var k = 0;
        for (var i = 0; i < dataLength.Max(); i++)
            for (var b = 0; b < blockCount; b++)
                if (i < dataLength[b]) blocks[b].Add(codewords[k++]);
        for (var i = 0; i < ecc; i++)
            for (var b = 0; b < blockCount; b++) blocks[b].Add(codewords[k++]);
        if (k != codewords.Length) throw new InvalidDataException("The codewords do not fill the blocks");

        // Every block, data then error correction, is a codeword of the code: zero at α^0 … α^(ecc-1).
        for (var b = 0; b < blockCount; b++)
        {
            for (var j = 0; j < ecc; j++)
            {
                var alpha = Power(j);
                var s = 0;
                foreach (var c in blocks[b]) s = Multiply(s, alpha) ^ c;
                if (s != 0) throw new InvalidDataException($"Block {b} fails its Reed–Solomon check");
            }
        }

        return Enumerable.Range(0, blockCount).SelectMany(b => blocks[b].Take(dataLength[b])).ToArray();
    }

    private static int Multiply(int x, int y)
    {
        var z = 0;
        while (y > 0)
        {
            if ((y & 1) != 0) z ^= x;
            x <<= 1;
            if ((x & 0x100) != 0) x ^= 0x11d;
            y >>= 1;
        }
        return z;
    }

    private static int Power(int e)
    {
        var v = 1;
        for (var i = 0; i < e; i++) v = Multiply(v, 2);
        return v;
    }

    private static string ReadByteMode(byte[] data, int version)
    {
        var at = 0;
        int Take(int count)
        {
            var v = 0;
            for (var i = 0; i < count; i++, at++) v = (v << 1) | ((data[at >> 3] >> (7 - (at & 7))) & 1);
            return v;
        }

        var capacity = data.Length * 8;
        var mode = Take(4);
        if (mode != 0b0100) throw new InvalidDataException($"Mode {mode} is not byte mode");
        var length = Take(version <= 9 ? 8 : 16);
        var bytes = new byte[length];
        for (var i = 0; i < length; i++) bytes[i] = (byte)Take(8);

        var terminator = Math.Min(4, capacity - at);
        if (Take(terminator) != 0) throw new InvalidDataException("The terminator is not zero");
        var padBits = (8 - (at % 8)) % 8;
        if (Take(padBits) != 0) throw new InvalidDataException("The bits up to the codeword boundary are not zero");
        for (var pad = 0xec; at < capacity; pad ^= 0xec ^ 0x11)
        {
            var got = Take(8);
            if (got != pad) throw new InvalidDataException($"Pad codeword {got:x2} where {pad:x2} belongs");
        }

        return new UTF8Encoding(false, true).GetString(bytes);
    }

    private static bool[,] Remasked(bool[,] unmasked, bool[,] function, int level, int mask)
    {
        var n = unmasked.GetLength(0);
        var m = (bool[,])unmasked.Clone();
        foreach (var (x, y) in DataOrder(n, function)) m[y, x] ^= Masked(mask, x, y);
        var bits = FormatBits((BitsOfLevel(level) << 3) | mask);
        for (var i = 0; i < 15; i++)
        {
            var on = ((bits >> i) & 1) != 0;
            m[FormatA(i).Y, FormatA(i).X] = on;
            m[FormatB(i, n).Y, FormatB(i, n).X] = on;
        }
        return m;
    }

    /// <summary>The four penalty rules of ISO/IEC 18004 §7.8.3.</summary>
    private static int Penalty(bool[,] m)
    {
        var n = m.GetLength(0);
        var score = 0;
        var dark = 0;
        for (var i = 0; i < n; i++)
        {
            var row = new bool[n];
            var column = new bool[n];
            for (var j = 0; j < n; j++)
            {
                row[j] = m[i, j];
                column[j] = m[j, i];
                if (m[i, j]) dark++;
            }
            score += Runs(row) + Runs(column) + FinderLike(row) + FinderLike(column);
        }
        for (var y = 0; y < n - 1; y++)
            for (var x = 0; x < n - 1; x++)
                if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) score += 3;

        // 10 for every whole 5% the dark share is away from half: |100·dark/n² − 50| / 5, in integers.
        var total = n * n;
        score += Math.Abs((20 * dark) - (10 * total)) / total * 10;
        return score;
    }

    private static int Runs(bool[] line)
    {
        var score = 0;
        var run = 1;
        for (var i = 1; i <= line.Length; i++)
        {
            if (i < line.Length && line[i] == line[i - 1])
            {
                run++;
                continue;
            }
            if (run >= 5) score += 3 + (run - 5);
            run = 1;
        }
        return score;
    }

    /// <summary>
    /// Dark-light-dark-dark-dark-light-dark with four light modules on either side, the symbol's edge
    /// counting as light; a match does not overlap the one before it.
    /// </summary>
    private static int FinderLike(bool[] line)
    {
        var n = line.Length;
        var score = 0;
        bool Light(int from, int to)
        {
            for (var i = Math.Max(from, 0); i < Math.Min(to, n); i++)
                if (line[i]) return false;
            return true;
        }
        var at = 0;
        while (at <= n - 7)
        {
            var pattern = line[at] && !line[at + 1] && line[at + 2] && line[at + 3] && line[at + 4] && !line[at + 5] && line[at + 6];
            if (!pattern)
            {
                at++;
                continue;
            }
            if (Light(at - 4, at) || Light(at + 7, at + 11))
            {
                score += 40;
                at += 7;
            }
            else
            {
                at += 4;
            }
        }
        return score;
    }
}
