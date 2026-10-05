using sb.Leaflets;
using sb.Songs;
using static sb.PermutationsAndCombinations;

namespace sb.Ordering;

/// <summary>
/// Determines the physical (on-page) order of a document's songs — distinct from each song's
/// singing-order number (FR-006/FR-007). "Space-efficient" minimizes real unused vertical space
/// under <see cref="ColumnBalancer"/>'s sequential column-fill algorithm (research.md §4/§5),
/// simulated directly via <see cref="ColumnBalancer.Fill"/> so the cost function can never drift
/// out of sync with how songs are actually packed (corrected 2026-09-27 — `/speckit-analyze`
/// finding U1: the previous cost function approximated a one-song-per-column row-pairing model
/// that no longer reflects how songs are packed). <see cref="Order"/> only ever decides *which
/// song comes before which* — never where a page/column break falls, which stays entirely
/// downstream in <see cref="ColumnBalancer"/>/<see cref="sb.Leaflets.LeafletBuilder"/> (FR-006,
/// research.md §16).
/// </summary>
public static class SongOrderer
{
    public static IReadOnlyList<Song> Order(IReadOnlyList<Song> songs, OrderingMode mode, PageMetrics pageMetrics, int maxPermutations = 100)
    {
        return mode switch
        {
            OrderingMode.AsListed => songs.ToList(),
            OrderingMode.SpaceEfficient => OrderSpaceEfficient(songs, pageMetrics, maxPermutations),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown ordering mode.")
        };
    }

    private static IReadOnlyList<Song> OrderSpaceEfficient(IReadOnlyList<Song> songs, PageMetrics pageMetrics, int maxPermutations)
    {
        if (songs.Count <= 1)
            return songs.ToList();

        var totalPermutations = nPr(songs.Count, songs.Count);
        return totalPermutations <= maxPermutations
            ? ExactSearch(songs, pageMetrics)
            : GreedyFallback(songs, pageMetrics);
    }

    /// <summary>Evaluates every permutation (deterministically ordered) and keeps the first one
    /// achieving the minimum real column-fill waste — this doubles as a correctness reference for
    /// the greedy path.</summary>
    private static IReadOnlyList<Song> ExactSearch(IReadOnlyList<Song> songs, PageMetrics pageMetrics)
    {
        List<Song>? best = null;
        var bestWaste = long.MaxValue;

        foreach (var permutation in Permute(songs.ToList()))
        {
            var waste = ComputeWastedSpace(permutation, pageMetrics);
            if (waste < bestWaste)
            {
                bestWaste = waste;
                best = permutation;
            }
        }

        return best!;
    }

    /// <summary>Deterministic order-preserving, simulation-driven heuristic (research.md §4,
    /// revised 2026-09-28, second correction): `space-efficient` mode should still track singing
    /// order as closely as possible, deviating from it only where doing so uses column space that
    /// would otherwise go to waste (spec.md FR-007's front-loading rule). At each step, evaluates
    /// placing every remaining song next by actually simulating <see cref="ColumnBalancer.Fill"/>
    /// on the resulting full candidate ordering (reusing <see cref="ComputeWastedSpace"/>, exactly
    /// like <see cref="ExactSearch"/> already does) and commits whichever minimizes total
    /// predicted waste — ties favor the earliest candidate (i.e. leaving the order alone), so
    /// nothing ever moves unless it actually helps. This replaces an earlier hand-rolled
    /// per-column capacity model that approximated column-fill without knowing about FR-025's
    /// even-page forced-break rule for over-long songs, and so could strand an entire column next
    /// to such a song without ever considering a smaller later song to fill it first — real-world
    /// evidence (`/speckit-clarify`, 2026-09-28): a 16-song chapter left an entire page's second
    /// column empty rather than pulling a later, shorter song into it, because the hand-rolled
    /// model treated inserting the next (over-long) song as unconditionally free regardless of
    /// page parity. Simulating the real algorithm every time, as this does, closes that class of
    /// drift for good rather than patching around one case. Costs O(n) steps × O(n) candidates ×
    /// O(n) simulation — fine at this project's realistic scale (spec.md: 10-50 songs per chapter;
    /// Constitution Principle II: build for the scale actually needed, not a speculative one).
    /// </summary>
    private static IReadOnlyList<Song> GreedyFallback(IReadOnlyList<Song> songs, PageMetrics pageMetrics)
    {
        var remaining = new List<Song>(songs);
        var result = new List<Song>(songs.Count);

        while (remaining.Count > 0)
        {
            var bestIndex = 0;
            var bestWaste = long.MaxValue;

            for (var i = 0; i < remaining.Count; i++)
            {
                var candidateOrder = BuildCandidateOrder(result, remaining, i);
                var waste = ComputeWastedSpace(candidateOrder, pageMetrics);
                if (waste < bestWaste)
                {
                    bestWaste = waste;
                    bestIndex = i;
                }
            }

            result.Add(remaining[bestIndex]);
            remaining.RemoveAt(bestIndex);
        }

        return result;
    }

    /// <summary>Builds `placed` followed by `remaining[candidateIndex]` followed by every other
    /// still-remaining song in its original relative order — the hypothetical full ordering
    /// <see cref="GreedyFallback"/> evaluates for one placement decision.</summary>
    private static List<Song> BuildCandidateOrder(List<Song> placed, List<Song> remaining, int candidateIndex)
    {
        var order = new List<Song>(placed.Count + remaining.Count);
        order.AddRange(placed);
        order.Add(remaining[candidateIndex]);
        for (var j = 0; j < remaining.Count; j++)
        {
            if (j != candidateIndex)
                order.Add(remaining[j]);
        }
        return order;
    }

    /// <summary>Simulates real column-fill packing for this ordering (via
    /// <see cref="ColumnBalancer.Fill"/>) and sums how much of each resulting page's total
    /// capacity (both columns) went unused — the single source of truth for "wasted space" also
    /// used to actually render the leaflet, so this can never drift out of sync with it.</summary>
    private static long ComputeWastedSpace(IReadOnlyList<Song> ordering, PageMetrics pageMetrics)
    {
        var blocks = ordering.Select(song => new SongBlock(new string[EstimateHeightInLines(song)])).ToList();
        var layout = ColumnBalancer.Fill(blocks, pageMetrics, startingPageNumber: 1);

        var linesPerPage = pageMetrics.LinesPerPage;
        return layout.Pages.Sum(page =>
        {
            // A page with no Right column has only ONE column's worth of real capacity — never
            // charge waste against a column that doesn't exist (a page like this renders as a
            // single full-width block, not a half-empty minipage).
            var used = page.Left.HeightInLines + (page.Right?.HeightInLines ?? 0);
            var capacity = page.Right is null ? linesPerPage : 2 * linesPerPage;
            return (long)capacity - used;
        });
    }

    /// <summary>A quick, order-independent estimate of a song's rendered height (title line, an
    /// optional melody line, one line per non-blank input line, two per blank one) — deliberately
    /// not modeling line-wrapping (Constitution Principle II: good enough to rank orderings by
    /// real column capacity, not a byte-exact match of the final rendered output).</summary>
    private static int EstimateHeightInLines(Song song)
    {
        var lines = 1; // title
        if (!string.IsNullOrWhiteSpace(song.Melody))
            lines++;

        foreach (var rawLine in song.Text.Replace("\r\n", "\n").Split('\n'))
            lines += rawLine.Trim().Length == 0 ? 2 : 1;

        return lines;
    }

    private static IEnumerable<List<Song>> Permute(List<Song> songs)
    {
        if (songs.Count <= 1)
        {
            yield return new List<Song>(songs);
            yield break;
        }

        for (var i = 0; i < songs.Count; i++)
        {
            var remaining = new List<Song>(songs);
            var chosen = remaining[i];
            remaining.RemoveAt(i);

            foreach (var rest in Permute(remaining))
            {
                var result = new List<Song> { chosen };
                result.AddRange(rest);
                yield return result;
            }
        }
    }
}
