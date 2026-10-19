using sb.Leaflets;
using sb.Songs;

namespace sb.Ordering;

/// <summary>
/// Determines the physical (on-page) order of a chapter's songs — distinct from each song's
/// singing-order number (FR-006/FR-007). "Space-efficient" is page-by-page First Fit Descending
/// (spec.md FR-007, research.md §4, 2026-10-19): the longest remaining song that fits goes on the
/// page first, the page is then topped up with the longest remaining songs that still fit, and
/// the placed songs leave the pool before the next page starts. "Fits" is decided by simulating
/// <see cref="ColumnBalancer.Fill"/>, so FR-025's page-parity rule is respected automatically.
/// <see cref="Order"/> only decides *which song comes before which* — never where a page/column
/// break falls, which stays downstream in <see cref="ColumnBalancer"/> (FR-006, research.md §16).
/// </summary>
public static class SongOrderer
{
    public static IReadOnlyList<Song> Order(IReadOnlyList<Song> songs, OrderingMode mode, PageMetrics pageMetrics)
    {
        return mode switch
        {
            OrderingMode.AsListed => songs.ToList(),
            OrderingMode.SpaceEfficient => OrderByFirstFitDescending(songs, pageMetrics),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown ordering mode.")
        };
    }

    private static IReadOnlyList<Song> OrderByFirstFitDescending(IReadOnlyList<Song> songs, PageMetrics pageMetrics)
    {
        // OrderByDescending is stable, so equal-length songs keep their original relative order.
        var pool = songs.OrderByDescending(EstimateHeightInLines).ToList();
        var result = new List<Song>(songs.Count);

        while (pool.Count > 0)
        {
            var startingPageNumber = 1 + PagesOccupiedBy(result, pageMetrics, 1, pageMetrics.ChapterHeadingLines);
            var headingLines = startingPageNumber == 1 ? pageMetrics.ChapterHeadingLines : 0;
            result.AddRange(FillNextPage(pool, pageMetrics, startingPageNumber, headingLines));
        }

        return result;
    }

    /// <summary>Takes the longest pool song that fits, then keeps taking the longest one that
    /// still fits the same page(s) until none does, removing every taken song from the pool. A
    /// song longer than a page always fits when it is the first one (it simply spans pages), and
    /// the space left on its last page is then filled like any other page's.</summary>
    private static List<Song> FillNextPage(List<Song> pool, PageMetrics pageMetrics, int startingPageNumber, int headingLines)
    {
        var page = new List<Song> { pool[0] };
        pool.RemoveAt(0);

        var fittedAnother = true;
        while (fittedAnother)
        {
            var pagesBefore = PagesOccupiedBy(page, pageMetrics, startingPageNumber, headingLines);
            var fitIndex = pool.FindIndex(song =>
                PagesOccupiedBy(page.Append(song).ToList(), pageMetrics, startingPageNumber, headingLines) == pagesBefore);

            fittedAnother = fitIndex >= 0;
            if (fittedAnother)
            {
                page.Add(pool[fitIndex]);
                pool.RemoveAt(fitIndex);
            }
        }

        return page;
    }

    /// <summary>How many physical pages this ordering occupies under real column-fill packing
    /// (via <see cref="ColumnBalancer.Fill"/>, the same algorithm that renders the leaflet).</summary>
    private static int PagesOccupiedBy(IReadOnlyList<Song> ordering, PageMetrics pageMetrics, int startingPageNumber, int headingLines)
    {
        var blocks = ordering.Select(song => new SongBlock(new string[EstimateHeightInLines(song)])).ToList();
        return ColumnBalancer.Fill(blocks, pageMetrics, startingPageNumber, headingLines).PagesConsumed;
    }

    /// <summary>A quick estimate of a song's rendered height (title line, an optional melody
    /// line, one line per input line — a blank one renders as one empty line) — deliberately not modeling
    /// line-wrapping (Constitution Principle II: good enough to rank songs by length and test fit,
    /// not a byte-exact match of the final rendered output).</summary>
    private static int EstimateHeightInLines(Song song)
    {
        var lines = 1; // title
        if (!string.IsNullOrWhiteSpace(song.Melody))
            lines++;

        // Leading/trailing blank lines are ignored by the builder (FR-003), so they add no height.
        var text = song.Text.Replace("\r\n", "\n").Trim();
        if (text.Length > 0)
            lines += text.Split('\n').Length;

        return lines;
    }
}
