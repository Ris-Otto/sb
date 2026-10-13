namespace Sb.Core;

/// <summary>
/// Splits a <see cref="SongFragment"/> so its first part fits within an available line budget,
/// carrying the rest forward as a titleless continuation (FR-1.3.4, FR-1.3.5). Works uniformly on
/// a fresh whole-song fragment or an already-partial continuation (e.g. a song spanning three
/// pages), since a continuation never re-gains a title.
///
/// Heights include everything rendered: the header on a title-carrying fragment, and the
/// <c>(forts.)</c> notice lines (FR-1.3.16) — one at the start of a fragment resuming from a
/// previous page and one at the end of a fragment that breaks off at a page boundary (A-5).
/// </summary>
public static class SongSplitter
{
    public static (SongFragment? First, SongFragment? Remainder) Split(
        SongFragment fragment, int availableLines, int columnWidth, bool isPageBoundary = false,
        bool allowPartialFirstParagraph = false)
    {
        if (availableLines >= fragment.Height)
            return (fragment, null);

        int titleLines = fragment.IncludesTitle ? SongLayout.HeaderLines(fragment.Song) : 0;
        int leadNoticeLines = fragment.ContinuesFromPreviousPage ? 1 : 0;
        int trailingNoticeLines = isPageBoundary ? 1 : 0; // only rendered because a split occurs
        int budget = availableLines - titleLines - leadNoticeLines - trailingNoticeLines;
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

        // A song may only start in this slot if at least one whole paragraph (lines up to an empty
        // line, or the end of the text) fits; otherwise it would begin with just a title and a stub
        // (A-2). The caller opts out for a fresh column, where waiting gains nothing.
        if (fragment.IncludesTitle && !allowPartialFirstParagraph
            && !HasCompleteParagraph(firstLines, fragment.Lines, splitIndex))
            return (null, null);

        if (!fragment.IncludesTitle && firstLines.Count == 0)
            return (null, null); // a titleless fragment with no lines would be empty

        // Empty lines at the break would only show up as a gap at the top of the next column/page.
        int restStart = splitIndex;
        while (restStart < fragment.Lines.Count && string.IsNullOrWhiteSpace(fragment.Lines[restStart]))
            restStart++;
        var restLines = fragment.Lines.Skip(restStart).ToList();

        if (restLines.Count == 0)
        {
            // Only empty lines were left over, so nothing continues: the song fits after all.
            return (fragment with { Lines = firstLines, Height = leadNoticeLines + titleLines + consumed }, null);
        }

        var first = fragment with
        {
            Lines = firstLines,
            Height = titleLines + leadNoticeLines + consumed + trailingNoticeLines,
            ContinuesOnNextPage = isPageBoundary,
        };
        var remainder = new SongFragment(
            fragment.Song, IncludesTitle: false, restLines,
            LineWrapper.CountOutputLines(restLines, columnWidth) + trailingNoticeLines,
            ContinuesFromPreviousPage: isPageBoundary);
        return (first, remainder);
    }

    // The paragraph is complete if an empty line follows content inside the placed lines, or the
    // placed lines end exactly where the next line is empty (or the text ends). The empty line itself
    // need not fit (A-5).
    private static bool HasCompleteParagraph(IReadOnlyList<string> placed, IReadOnlyList<string> all, int splitIndex)
    {
        bool sawContent = false;
        foreach (string line in placed)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (sawContent)
                    return true;
            }
            else
            {
                sawContent = true;
            }
        }

        return sawContent && (splitIndex >= all.Count || string.IsNullOrWhiteSpace(all[splitIndex]));
    }
}
