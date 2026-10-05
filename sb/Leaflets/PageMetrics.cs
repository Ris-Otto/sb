namespace sb.Leaflets;

/// <summary>
/// Page/column layout capacity derived from a document template's own geometry (FR-026,
/// research.md §14) — never an independently maintained configuration value, so pagination
/// decisions stay in sync with whatever template actually renders the output.
/// </summary>
public sealed class PageMetrics
{
    public double PaperHeightPt { get; }
    public double TopMarginPt { get; }
    public double BottomMarginPt { get; }
    public double BaselineSkipPt { get; }

    public double UsableHeightPt => PaperHeightPt - TopMarginPt - BottomMarginPt;

    /// <summary>The per-column capacity research.md §5's column-fill algorithm packs against.</summary>
    public int LinesPerPage => (int)Math.Floor(UsableHeightPt / BaselineSkipPt);

    public PageMetrics(double paperHeightPt, double topMarginPt, double bottomMarginPt, double baselineSkipPt)
    {
        PaperHeightPt = paperHeightPt;
        TopMarginPt = topMarginPt;
        BottomMarginPt = bottomMarginPt;
        BaselineSkipPt = baselineSkipPt;
    }
}
