namespace Sb.Core;

/// <summary>
/// Splits a <see cref="SongFragment"/> so its first part fits within an available line budget,
/// carrying the rest forward as a titleless continuation (FR-1.3.4, FR-1.3.5). Works uniformly on
/// a fresh whole-song fragment or an already-partial continuation (e.g. a song spanning three
/// pages), since a continuation never re-gains a title.
/// </summary>
public static class SongSplitter
{
    public static (SongFragment? First, SongFragment? Remainder) Split(
        SongFragment fragment, int availableLines, int columnWidth, bool isPageBoundary = false)
    {
        if (availableLines >= fragment.Height)
            return (fragment, null);

        int titleLines = fragment.IncludesTitle ? 1 : 0;
        int budget = availableLines - titleLines;
        if (budget < 0)
            return (null, null);

        var firstLines = new List<string>();
        int consumed = 0;
        int splitIndex = 0;
        for (; splitIndex < fragment.Lines.Count; splitIndex++)
        {
            int lineHeight = LineWrapper.CountOutputLines(fragment.Lines[splitIndex], columnWidth);
            if (consumed + lineHeight > budget)
                break;
            firstLines.Add(fragment.Lines[splitIndex]);
            consumed += lineHeight;
        }

        if (!fragment.IncludesTitle && firstLines.Count == 0)
            return (null, null); // a titleless fragment with no lines would be empty

        var restLines = fragment.Lines.Skip(splitIndex).ToList();
        var first = fragment with { Lines = firstLines, Height = titleLines + consumed, ContinuesOnNextPage = isPageBoundary };
        var remainder = new SongFragment(
            fragment.Song, IncludesTitle: false, restLines, LineWrapper.CountOutputLines(restLines, columnWidth),
            ContinuesFromPreviousPage: isPageBoundary);
        return (first, remainder);
    }
}
