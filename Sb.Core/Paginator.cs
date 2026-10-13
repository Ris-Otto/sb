namespace Sb.Core;

/// <summary>
/// Lays a sequence of song fragments out across pages, applying the overflow caveats (§2.2): a
/// song may always split across a page's own column 1→2 boundary (handled by
/// <see cref="ColumnFiller"/>), and — only on an odd (left-side) page — may additionally split
/// across the page boundary into the next page's column 1 (FR-1.3.5). On an even page, a
/// non-fitting song instead moves wholly to the next page (FR-1.3.6).
/// </summary>
public static class Paginator
{
    public static IReadOnlyList<Page> PaginateAsListed(
        IReadOnlyList<Song> songs, int linesPerColumn, int columnWidth, int? firstPageLinesPerColumn = null)
    {
        var candidates = songs.Select(song => SongFragment.Whole(song, columnWidth)).ToList();
        return Paginate(candidates, linesPerColumn, columnWidth, firstPageLinesPerColumn);
    }

    public static IReadOnlyList<Page> Paginate(
        IReadOnlyList<SongFragment> initialCandidates, int linesPerColumn, int columnWidth,
        int? firstPageLinesPerColumn = null)
    {
        var pages = new List<Page>();
        IReadOnlyList<SongFragment> remaining = initialCandidates;
        int pageNumber = 1;

        while (remaining.Count > 0)
        {
            bool isOddPage = pageNumber % 2 == 1;
            // A chapter's first page is shorter: its heading takes room the other pages have (A-12).
            int pageLines = pageNumber == 1 ? firstPageLinesPerColumn ?? linesPerColumn : linesPerColumn;
            PageLayout layout = ColumnFiller.Fill(remaining, pageLines, columnWidth, allowPageOverflow: isOddPage);
            var column2 = layout.Column2.ToList();
            int column2Lines = layout.Column2Lines;
            IReadOnlyList<SongFragment> nextRemaining = layout.RemainingFragments;

            if (isOddPage && layout.RemainingFragments.Count > 0)
            {
                int leftover = pageLines - column2Lines;
                if (leftover > 0)
                {
                    var (first, remainder) = SongSplitter.Split(
                        layout.RemainingFragments[0], leftover, columnWidth, isPageBoundary: true);
                    if (first is not null)
                    {
                        column2.Add(first);
                        column2Lines += first.Height;

                        var afterFirst = layout.RemainingFragments.Skip(1).ToList();
                        nextRemaining = remainder is not null ? Prepend(remainder, afterFirst) : afterFirst;
                    }
                }
            }

            pages.Add(new Page(pageNumber, layout.Column1, column2, layout.Column1Lines, column2Lines));
            remaining = nextRemaining;
            pageNumber++;
        }

        return pages;
    }

    private static IReadOnlyList<SongFragment> Prepend(SongFragment fragment, IReadOnlyList<SongFragment> rest)
    {
        var list = new List<SongFragment> { fragment };
        list.AddRange(rest);
        return list;
    }
}
