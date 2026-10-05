namespace Sb.Core;

/// <summary>
/// Simulates filling one page's two columns with as many whole songs as fit (FR-1.2.4), allowing
/// exactly one song to overflow from column 1 into column 2 (FR-1.2.5 — the space-efficient
/// caller is expected to have already picked which songs go on this page; this just mechanically
/// places them and reports the result, including any leftover space (FR-1.2.6) — it does not judge
/// whether that leftover is acceptable, since that depends on whether this is a chapter's last page.
/// </summary>
public static class ColumnFiller
{
    public static PageLayout Fill(IReadOnlyList<Song> songs, int linesPerColumn, int columnWidth)
    {
        var column1 = new List<Song>();
        var column2 = new List<Song>();
        int column1Lines = 0;
        int column2Lines = 0;
        int index = 0;

        while (index < songs.Count)
        {
            int height = SongLayout.Height(songs[index], columnWidth);
            if (column1Lines + height > linesPerColumn)
                break;

            column1.Add(songs[index]);
            column1Lines += height;
            index++;
        }

        if (index < songs.Count)
        {
            int height = SongLayout.Height(songs[index], columnWidth);
            int remainingColumn1 = linesPerColumn - column1Lines;
            // overflowIntoColumn2 is guaranteed > 0 here: the loop above only stopped because this
            // song didn't fit (height > remainingColumn1), i.e. it already failed to fit wholly.
            int overflowIntoColumn2 = height - remainingColumn1;

            if (remainingColumn1 > 0 && overflowIntoColumn2 <= linesPerColumn)
            {
                column1.Add(songs[index]);
                column2.Add(songs[index]);
                column1Lines = linesPerColumn;
                column2Lines = overflowIntoColumn2;
                index++;
            }
        }

        while (index < songs.Count)
        {
            int height = SongLayout.Height(songs[index], columnWidth);
            if (column2Lines + height > linesPerColumn)
                break;

            column2.Add(songs[index]);
            column2Lines += height;
            index++;
        }

        return new PageLayout(
            column1, column2, column1Lines, column2Lines, linesPerColumn - column2Lines, songs.Skip(index).ToList());
    }
}
