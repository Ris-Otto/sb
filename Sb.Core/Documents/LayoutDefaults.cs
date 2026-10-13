namespace Sb.Core.Documents;

/// <summary>
/// How many characters and lines fit a column of the page that <c>template-main.tex</c> sets up, worked
/// out from that template's page geometry rather than guessed (A-12). The inputs below were measured by
/// compiling the template with LuaLaTeX (A4, 0.4in side margins, 0.3in top and 0.65in bottom margin,
/// Lexend at 12pt on 14.4pt leading, two minipage columns of 0.48\textwidth) and asking LaTeX for the
/// sizes involved. If the template's geometry or font changes, they have to be measured again.
///
/// Planning is done in whole lines (<see cref="LineWrapper"/>, <see cref="SongLayout"/>), so the page is
/// reduced to a line count:
/// <list type="bullet">
/// <item>a column of <c>n</c> lines is <c>n × 14.4pt − 5.0pt</c> tall (the space before the first
/// song's title is a little smaller than a line), so at most 54 lines fit the 776.4pt text height;</item>
/// <item>the first page of a chapter also holds the <c>\chapter*</c> heading, 135.7pt, which leaves room
/// for 44;</item>
/// <item>one line of safety is kept on both, because a lyric line that comes out wider than expected
/// makes LaTeX wrap it, which the plan does not know about;</item>
/// <item>a column is 259.05pt wide; of the lyrics measured (312 lines of real songs) none of the lines up
/// to 44 characters overflowed it and the first that did was 45, and 5.96pt is the widest per-character
/// width of the lines of 40–45 characters, which gives 43.</item>
/// </list>
/// Shared between <see cref="DocumentOperations"/> and <see cref="ChapterOperations"/> so both lay out
/// content against the same assumed page geometry.
/// </summary>
internal static class LayoutDefaults
{
    private const double TextHeightPt = 776.39;
    private const double LineHeightPt = 14.4;

    /// <summary>The first title of a column sits 5.05pt higher than a whole line would put it.</summary>
    private const double FirstLineGainPt = 5.05;

    private const double ChapterHeadingPt = 135.72;
    private const double ColumnWidthPt = 259.05;
    private const double WidestCharWidthPt = 5.96;
    private const int SafetyLines = 1;

    /// <summary>Lines that fit a column on every page except a chapter's first.</summary>
    public static readonly int LinesPerColumn =
        (int)Math.Floor((TextHeightPt + FirstLineGainPt) / LineHeightPt) - SafetyLines;

    /// <summary>Lines that fit a column on a chapter's first page, below the chapter heading.</summary>
    public static readonly int FirstPageLinesPerColumn =
        (int)Math.Floor((TextHeightPt - ChapterHeadingPt + FirstLineGainPt) / LineHeightPt) - SafetyLines;

    /// <summary>Characters per line before a line has to be wrapped.</summary>
    public static readonly int ColumnWidth = (int)Math.Floor(ColumnWidthPt / WidestCharWidthPt);
}
