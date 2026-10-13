namespace Sb.Core;

/// <summary>
/// Space-efficient pagination (§2.2): each page starts with the longest remaining song
/// (FR-1.3.8), then fills the rest of the page from the remaining pool (<c>ChooseCompanions</c>):
/// First-Fit-Decreasing (<see cref="FirstFitDecreasing"/>) first, then the exact subset-sum search
/// (<see cref="SubsetSumSearch"/>) at several capacities (FR-1.3.9, FR-1.3.12), with every candidate
/// set scored by actually placing it with <see cref="ColumnFiller"/> so the paragraph rule
/// (FR-1.3.17) and the even-page rule (FR-1.3.6) are accounted for (FR-1.3.18). The overflow caveats
/// (§2.2) apply: a song may split across column 1→2, or, on an odd page, across the page boundary
/// (FR-1.3.5) — here, by offering the next pool song as a candidate to fill a page's remaining
/// leftover.
/// </summary>
public static class SpaceEfficientPaginator
{
    public static IReadOnlyList<Page> Paginate(
        IReadOnlyList<Song> songs, int linesPerColumn, int columnWidth, int? firstPageLinesPerColumn = null)
    {
        var pages = new List<Page>();
        var pool = songs.ToList();
        SongFragment? carryOver = null;
        int pageNumber = 1;

        while (pool.Count > 0 || carryOver is not null)
        {
            bool isOddPage = pageNumber % 2 == 1;
            var pageCandidates = new List<SongFragment>();
            // A chapter's first page is shorter: its heading takes room the other pages have (A-12).
            int pageLines = pageNumber == 1 ? firstPageLinesPerColumn ?? linesPerColumn : linesPerColumn;
            int capacity = 2 * pageLines;

            if (carryOver is not null)
            {
                pageCandidates.Add(carryOver);
                capacity -= carryOver.Height;
                carryOver = null;
            }

            if (pool.Count > 0 && capacity > 0)
            {
                Song longest = pool.OrderByDescending(song => SongLayout.Height(song, columnWidth)).First();
                pool.Remove(longest);
                pageCandidates.Add(SongFragment.Whole(longest, columnWidth));

                int capacityForOthers = capacity - SongLayout.Height(longest, columnWidth);
                if (capacityForOthers > 0 && pool.Count > 0)
                {
                    foreach (Song song in ChooseCompanions(
                        pageCandidates, pool, capacityForOthers, pageLines, columnWidth, isOddPage))
                    {
                        pageCandidates.Add(SongFragment.Whole(song, columnWidth));
                        pool.Remove(song);
                    }
                }
            }

            // The selection totals <= 2*linesPerColumn by height, but placement can still leave
            // fragments over: an oversized carry-over, a song that can't start where only a stub would
            // fit (FR-1.3.17), or a song that may not spill out of an even page (FR-1.3.6). All of
            // these are handled below rather than assumed away.
            PageLayout layout = ColumnFiller.Fill(pageCandidates, pageLines, columnWidth, allowPageOverflow: isOddPage);
            var column2 = layout.Column2.ToList();
            int column2Lines = layout.Column2Lines;

            if (layout.RemainingFragments.Count > 0)
            {
                // Carry the unplaced piece forward rather than silently dropping it (NFR-2.5.3).
                // Everything after it is still a whole, unsplit fragment (only a split song's excess
                // is mid-song, and that is always first), so it's safe to return those songs to the
                // pool for a fresh attempt on a later page.
                carryOver = layout.RemainingFragments[0];
                pool.AddRange(layout.RemainingFragments.Skip(1).Select(fragment => fragment.Song));
            }
            else if (isOddPage && pool.Count > 0)
            {
                int leftover = pageLines - column2Lines;
                if (leftover > 0)
                {
                    Song candidate = pool[0];
                    var (first, remainder) = SongSplitter.Split(
                        SongFragment.Whole(candidate, columnWidth), leftover, columnWidth, isPageBoundary: true);
                    if (first is not null)
                    {
                        column2.Add(first);
                        column2Lines += first.Height;
                        pool.RemoveAt(0);
                        carryOver = remainder;
                    }
                }
            }

            pages.Add(new Page(pageNumber, layout.Column1, column2, layout.Column1Lines, column2Lines));
            pageNumber++;
        }

        return pages;
    }

    /// <summary>
    /// Picks the pool songs to share a page with <paramref name="lead"/>. Candidate sets come from
    /// FFD and from the exact subset-sum search at a range of capacities (a set that sums
    /// to the capacity can still be unplaceable, since a song may only start where a whole paragraph
    /// fits — FR-1.3.17), and each is tried in a few orders. Every attempt is scored by actually
    /// placing it with <see cref="ColumnFiller"/>, so the winner is the one that really fills the most
    /// lines, not merely the one whose heights add up best.
    /// </summary>
    private static IReadOnlyList<Song> ChooseCompanions(
        IReadOnlyList<SongFragment> lead, IReadOnlyList<Song> pool, int capacity, int linesPerColumn, int columnWidth,
        bool isOddPage)
    {
        int pageCapacity = 2 * linesPerColumn;
        var attempted = new HashSet<string>();
        IReadOnlyList<Song> best = [];
        int bestScore = -1;

        bool TryOrdered(IReadOnlyList<Song> set)
        {
            foreach (IReadOnlyList<Song> ordered in Orderings(set, columnWidth))
            {
                string key = string.Join(",", ordered.Select(song => pool.ToList().IndexOf(song)));
                if (!attempted.Add(key))
                    continue;

                var candidates = lead.Concat(ordered.Select(song => SongFragment.Whole(song, columnWidth))).ToList();
                PageLayout layout = ColumnFiller.Fill(candidates, linesPerColumn, columnWidth, allowPageOverflow: isOddPage);
                int score = layout.Column1Lines + layout.Column2Lines;
                if (score > bestScore)
                {
                    best = ordered;
                    bestScore = score;
                }
            }

            return bestScore >= pageCapacity * SpaceEfficiency.FillTarget;
        }

        // Fast path (FR-1.3.12): FFD first; if the page actually fills to target, no further search.
        if (TryOrdered(FirstFitDecreasing.Fill(pool, capacity, columnWidth).Selected))
            return best;

        // Exact search at the full capacity and at progressively smaller ones; the smaller ones find
        // sets that leave slack for the paragraph rule to be satisfied.
        for (int cap = capacity; cap >= Math.Max(1, capacity - linesPerColumn); cap--)
        {
            if (TryOrdered(SubsetSumSearch.FindBestFit(pool, cap, columnWidth).Selected))
                break;
        }

        return best;
    }

    private static IEnumerable<IReadOnlyList<Song>> Orderings(IReadOnlyList<Song> set, int columnWidth)
    {
        yield return set;
        yield return set.OrderByDescending(song => SongLayout.Height(song, columnWidth)).ToList();
        yield return set.OrderBy(song => SongLayout.Height(song, columnWidth)).ToList();
    }
}
