namespace sb.Leaflets;

/// <summary>
/// One chapter's column-fill result (research.md §5) — used both to render its `chapter.tex` and
/// to hand off the running physical page counter to the next chapter (FR-025, research.md §15).
/// </summary>
public sealed class PageLayout
{
    /// <summary>One entry per physical page this chapter's content occupies, in order. <c>Right</c>
    /// is <c>null</c> only for a page whose column-fill never reached a second column (e.g. the
    /// chapter's last page, if its content runs out partway through the left column).</summary>
    public IReadOnlyList<(Column Left, Column? Right)> Pages { get; }

    /// <summary>How many physical pages this chapter occupied — a page counts as consumed as soon
    /// as any content lands on it, even if only partially filled (there is no half-page unit),
    /// so the next chapter's starting-page parity for FR-025 is never miscounted.</summary>
    public int PagesConsumed => Pages.Count;

    public PageLayout(IReadOnlyList<(Column Left, Column? Right)> pages)
    {
        Pages = pages;
    }
}
