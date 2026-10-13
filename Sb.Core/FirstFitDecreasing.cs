namespace Sb.Core;

/// <summary>
/// The fast path of the space-efficient search (FR-1.3.12): sort candidates longest-first and
/// greedily add each while it still fits, skipping (not stopping at) ones that don't. Used as the
/// first candidate set for a page; its real placement is then measured (FR-1.3.18) and the exact
/// search (<see cref="SubsetSumSearch"/>) is only tried when the page it produces misses the fill
/// target. <see cref="FfdResult.MetTarget"/> only reflects the summed heights, not that placement.
/// </summary>
public static class FirstFitDecreasing
{
    public static FfdResult Fill(IReadOnlyList<Song> candidates, int capacity, int columnWidth)
    {
        var sorted = candidates.OrderByDescending(song => SongLayout.Height(song, columnWidth)).ToList();
        var selected = new List<Song>();
        int total = 0;

        foreach (Song song in sorted)
        {
            int height = SongLayout.Height(song, columnWidth);
            if (total + height > capacity)
                continue;

            selected.Add(song);
            total += height;
        }

        bool metTarget = total >= capacity * SpaceEfficiency.FillTarget;
        return new FfdResult(selected, total, metTarget);
    }
}
