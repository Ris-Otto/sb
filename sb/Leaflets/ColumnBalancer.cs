namespace sb.Leaflets;

/// <summary>
/// Fills physical columns *sequentially* with as many whole <see cref="SongBlock"/>s as fit per
/// <see cref="PageMetrics.LinesPerPage"/> — not one song per column per row (research.md §5,
/// corrected 2026-09-27 per `/speckit-analyze` finding U1: a column has no song-count limit). A
/// <see cref="SongBlock"/> too big to fit in one column on its own is chunked across as many
/// further columns/pages as needed (FR-025, research.md §15) — the same "doesn't fit, continue
/// elsewhere" behavior applied to any song, not a separate mechanism. The only additional rule:
/// such a song starting on an EVEN-numbered physical page must start at the top of that page's
/// left column (a page break is forced before it if it wouldn't otherwise land there); no such
/// forcing applies on an ODD-numbered page. Once an over-long song's content is exhausted,
/// column-filling with subsequent songs resumes normally — a page/column is never reserved
/// exclusively for one song.
///
/// A song's title/melody and its lyrics always render together in the same minipage as one
/// unbroken block (reverted 2026-09-27, direct instruction — a prior design split a fresh
/// column's leading title into its own row, matching `test.tex`/`Kottar i karburatorn.tex`; that
/// split is no longer how this project renders columns).
/// </summary>
public static class ColumnBalancer
{
    private const string LeftWidth = ".475\\textwidth";
    private const string RightWidth = ".45\\textwidth";
    private const string Gutter = "0.075\\textwidth";

    public static PageLayout Fill(IReadOnlyList<SongBlock> songBlocks, PageMetrics pageMetrics, int startingPageNumber, int firstPageReservedLines = 0)
    {
        var linesPerPage = pageMetrics.LinesPerPage;
        var pages = new List<(Column Left, Column? Right)>();

        // The chapter heading occupies the top of the chapter's first page (both of its columns),
        // so only that page has less room; every later page has the full capacity.
        int ColumnCapacity() => pages.Count == 0 ? linesPerPage - firstPageReservedLines : linesPerPage;

        var pageNumber = startingPageNumber;
        var fillingLeft = true;
        Column? pendingLeft = null;
        var currentLines = new List<string>();

        void FinishColumn()
        {
            if (fillingLeft)
            {
                pendingLeft = new Column(currentLines.ToArray());
                currentLines = new List<string>();
                fillingLeft = false;
            }
            else
            {
                var right = currentLines.Count > 0 ? new Column(currentLines.ToArray()) : null;
                currentLines = new List<string>();
                pages.Add((pendingLeft!, right));
                pendingLeft = null;
                fillingLeft = true;
                pageNumber++;
            }
        }

        void ForceBreakToFreshPage()
        {
            if (currentLines.Count > 0)
                FinishColumn();
            if (!fillingLeft)
                FinishColumn();
        }

        void PlaceBlock(SongBlock block)
        {
            // A song that fits a full column but not this (heading-reduced) one is never split
            // unless the column is still empty — it moves on to the next column instead.
            if (block.HeightInLines > ColumnCapacity() && block.HeightInLines <= linesPerPage && currentLines.Count > 0)
            {
                FinishColumn();
                PlaceBlock(block);
                return;
            }

            if (block.HeightInLines <= ColumnCapacity())
            {
                // Whole-song placement: move to a fresh column if it doesn't fit in what's left —
                // never split a song that could fit entirely in an empty column.
                if (currentLines.Count + block.HeightInLines > ColumnCapacity())
                    FinishColumn();

                currentLines.AddRange(block.Lines);
                return;
            }

            // Over-long song: chunk across as many columns as needed, filling whatever's left in
            // the current column first, then continuing through full columns.
            var remaining = block.Lines;
            while (remaining.Count > 0)
            {
                var spaceLeft = ColumnCapacity() - currentLines.Count;
                if (spaceLeft <= 0)
                {
                    FinishColumn();
                    spaceLeft = ColumnCapacity();
                }

                var takeCount = Math.Min(spaceLeft, remaining.Count);
                currentLines.AddRange(remaining.Take(takeCount));
                remaining = remaining.Skip(takeCount).ToList();
            }
        }

        foreach (var block in songBlocks)
        {
            var atTopOfFreshPage = fillingLeft && currentLines.Count == 0;
            if (block.HeightInLines > linesPerPage && !atTopOfFreshPage && pageNumber % 2 == 0)
                ForceBreakToFreshPage();

            PlaceBlock(block);
        }

        if (currentLines.Count > 0)
            FinishColumn();
        if (pendingLeft is not null)
            pages.Add((pendingLeft, null));

        return new PageLayout(pages);
    }

    public static string Render(PageLayout layout) =>
        string.Join("\n", layout.Pages.Select(RenderPage));

    private static string RenderPage((Column Left, Column? Right) page) =>
        page.Right is null
            ? RenderSingleColumn(page.Left)
            : RenderRow(page.Left.Lines, LeftWidth, page.Right.Lines, RightWidth);

    /// <summary>Renders one row of (up to) two side-by-side minipages, `\vspace`-balancing
    /// whichever is shorter (FR-005/SC-003). Both minipages use the `[t]` (top) alignment
    /// specifier — without it, LaTeX defaults to `[c]` (vertically centered), which does not
    /// reliably top-align two boxes of different heights even once their total heights are
    /// equalized via the trailing `\vspace` below (verified against a real compiled PDF: with the
    /// default `[c]`, the shorter column's first line still landed roughly one `\baselineskip`
    /// below its sibling's). `[t]` anchors each box's own top-left corner to the current line, so
    /// both columns' first lines land at exactly the same height regardless of content-height
    /// differences; the `\vspace` padding remains solely to even out where the two columns'
    /// *bottoms* land. If only one side has content, it still renders in its own designated-width
    /// minipage, just without a gutter/partner (still column-width, not full-width).</summary>
    private static string RenderRow(IReadOnlyList<string> left, string leftWidth, IReadOnlyList<string> right, string rightWidth)
    {
        if (left.Count == 0 && right.Count == 0)
            return "";
        if (right.Count == 0)
            return RenderLoneMinipage(left, leftWidth, precedingOffsetWidth: null);
        if (left.Count == 0)
            return RenderLoneMinipage(right, rightWidth, precedingOffsetWidth: leftWidth);

        var heightDifference = left.Count - right.Count;
        var leftVspace = heightDifference < 0 ? BuildVspace(-heightDifference) : string.Empty;
        var rightVspace = heightDifference > 0 ? BuildVspace(heightDifference) : string.Empty;

        return
            $"\\noindent\n\\begin{{minipage}}[t]{{{leftWidth}}}\n{string.Join("\n", left)}{leftVspace}\n\\end{{minipage}}%\n" +
            $"\\hspace{{{Gutter}}}\n\\noindent\n\\begin{{minipage}}[t]{{{rightWidth}}}\n{string.Join("\n", right)}{rightVspace}\n\\end{{minipage}}\n";
    }

    private static string RenderSingleColumn(Column column) =>
        $"\\noindent\n{string.Join("\n", column.Lines)}\n";

    /// <summary>A lone minipage with no sibling on the same row. <paramref name="precedingOffsetWidth"/>
    /// is the sibling column's own width — non-null exactly when this lone box is the RIGHT
    /// column (nothing precedes it in the source to naturally push it right of the gutter, unlike
    /// the left column, which starts at the page's left margin on its own). Without this, a lone
    /// right-side box renders flush against the left margin instead of under the right column
    /// (the bug behind a "stray"-looking line reported in real-world testing). Also `[t]`-aligned
    /// (see <see cref="RenderRow"/>).</summary>
    private static string RenderLoneMinipage(IReadOnlyList<string> lines, string width, string? precedingOffsetWidth)
    {
        var offset = precedingOffsetWidth is null ? "" : $"\\hspace{{{precedingOffsetWidth}}}\\hspace{{{Gutter}}}";
        return $"\\noindent{offset}\n\\begin{{minipage}}[t]{{{width}}}\n{string.Join("\n", lines)}\n\\end{{minipage}}\n";
    }

    private static string BuildVspace(int lineCount) => $"\n\\vspace{{{lineCount}\\baselineskip}}";
}
