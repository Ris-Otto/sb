namespace Sb.Core;

/// <summary>
/// Simulates filling one page's two columns with as many whole songs as fit (FR-1.2.4), allowing
/// a song to split across the column 1→2 boundary (FR-1.2.5, via <see cref="SongSplitter"/>) —
/// including a song too tall for a single column, which fills column 1 AND column 2 in sequence,
/// carrying only the genuine excess (beyond both full columns) forward. The space-efficient caller
/// is expected to have already picked which songs go on this page; this just mechanically places
/// them and reports the result, including any leftover space (FR-1.2.6) — it does not judge whether
/// that leftover is acceptable, since that depends on whether this is a chapter's last page.
/// </summary>
public static class ColumnFiller
{
    public static PageLayout Fill(IReadOnlyList<SongFragment> candidates, int linesPerColumn, int columnWidth)
    {
        if (linesPerColumn <= 0)
            throw new ArgumentOutOfRangeException(nameof(linesPerColumn), "Must be positive.");

        var column1 = new List<SongFragment>();
        var column2 = new List<SongFragment>();
        int column1Lines = 0;
        int column2Lines = 0;
        int index = 0;

        while (index < candidates.Count)
        {
            if (column1Lines + candidates[index].Height > linesPerColumn)
                break;

            column1.Add(candidates[index]);
            column1Lines += candidates[index].Height;
            index++;
        }

        if (index < candidates.Count)
        {
            int remainingColumn1 = linesPerColumn - column1Lines;
            if (remainingColumn1 > 0)
            {
                var (firstPart, afterColumn1) = SongSplitter.Split(candidates[index], remainingColumn1, columnWidth);
                if (firstPart is not null)
                {
                    column1.Add(firstPart);
                    column1Lines += firstPart.Height;
                    index++;

                    if (afterColumn1 is not null)
                    {
                        // Column 2 is the last column on this page — anything that doesn't fit it
                        // necessarily waits for the next page, so this split is always page-boundary.
                        var (secondPart, excess) =
                            SongSplitter.Split(afterColumn1, linesPerColumn, columnWidth, isPageBoundary: true);
                        if (secondPart is not null)
                        {
                            column2.Add(secondPart);
                            column2Lines += secondPart.Height;
                        }

                        if (excess is not null)
                        {
                            // Genuinely too long for even a full page: stop here and carry the true
                            // excess forward as the next page's lead candidate.
                            return new PageLayout(
                                column1, column2, column1Lines, column2Lines, linesPerColumn - column2Lines,
                                Prepend(excess, candidates.Skip(index).ToList()));
                        }
                    }
                }
            }
        }

        while (index < candidates.Count)
        {
            if (column2Lines + candidates[index].Height > linesPerColumn)
                break;

            column2.Add(candidates[index]);
            column2Lines += candidates[index].Height;
            index++;
        }

        return new PageLayout(
            column1, column2, column1Lines, column2Lines, linesPerColumn - column2Lines,
            candidates.Skip(index).ToList());
    }

    private static IReadOnlyList<SongFragment> Prepend(SongFragment fragment, IReadOnlyList<SongFragment> rest)
    {
        var list = new List<SongFragment> { fragment };
        list.AddRange(rest);
        return list;
    }
}
