namespace Sb.Core;

/// <summary>
/// Space-efficient pagination (§2.2): each page starts with the longest remaining song
/// (FR-1.3.8), then fills the rest of the page's capacity from the remaining pool via
/// First-Fit-Decreasing (<see cref="FirstFitDecreasing"/>), falling back to the exact subset-sum
/// search (<see cref="SubsetSumSearch"/>) when FFD doesn't reach the 95% fill target (FR-1.3.9,
/// FR-1.3.12). The overflow caveats (§2.2) still apply: a song may split across column 1→2
/// (<see cref="ColumnFiller"/>) or, on an odd page, across the page boundary (FR-1.3.5) — here, by
/// offering the next pool song as a candidate to fill a page's remaining leftover.
/// </summary>
public static class SpaceEfficientPaginator
{
    public static IReadOnlyList<Page> Paginate(IReadOnlyList<Song> songs, int linesPerColumn, int columnWidth)
    {
        var pages = new List<Page>();
        var pool = songs.ToList();
        SongFragment? carryOver = null;
        int pageNumber = 1;

        while (pool.Count > 0 || carryOver is not null)
        {
            var pageCandidates = new List<SongFragment>();
            int capacity = 2 * linesPerColumn;

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
                    var ffd = FirstFitDecreasing.Fill(pool, capacityForOthers, columnWidth);
                    IReadOnlyList<Song> chosen = ffd.MetTarget
                        ? ffd.Selected
                        : SubsetSumSearch.FindBestFit(pool, capacityForOthers, columnWidth).Selected;

                    foreach (Song song in chosen)
                    {
                        pageCandidates.Add(SongFragment.Whole(song, columnWidth));
                        pool.Remove(song);
                    }
                }
            }

            // The selection above always totals <= 2*linesPerColumn, so the only way
            // layout.RemainingFragments is non-empty is an oversized carry-over (a song whose
            // continuation alone exceeds a full page) — handled below rather than assumed away.
            PageLayout layout = ColumnFiller.Fill(pageCandidates, linesPerColumn, columnWidth);
            var column2 = layout.Column2.ToList();
            int column2Lines = layout.Column2Lines;
            bool isOddPage = pageNumber % 2 == 1;

            if (layout.RemainingFragments.Count > 0)
            {
                // Carry the unplaced piece forward rather than silently dropping it (NFR-2.5.3).
                // Everything after it is guaranteed to still be a whole, unsplit fragment (only the
                // lead carry-over can be mid-song here), so it's safe to return those songs to the
                // pool for a fresh longest-first/FFD/exact-search attempt on a later page.
                carryOver = layout.RemainingFragments[0];
                pool.AddRange(layout.RemainingFragments.Skip(1).Select(fragment => fragment.Song));
            }
            else if (isOddPage && pool.Count > 0)
            {
                int leftover = linesPerColumn - column2Lines;
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
}
