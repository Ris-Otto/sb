namespace Sb.Core;

/// <summary>
/// Computes the cosmetic `\vspace` correction (FR-1.2.7) needed so both columns on a page start at
/// the same line — a finishing touch applied to the shorter column, not the primary mechanism for
/// using available space (that's <see cref="ColumnFiller"/>'s job).
/// </summary>
public static class VerticalAlignment
{
    public static VSpaceCorrection Compute(int column1Lines, int column2Lines) =>
        column1Lines.CompareTo(column2Lines) switch
        {
            < 0 => new VSpaceCorrection(column2Lines - column1Lines, 0),
            > 0 => new VSpaceCorrection(0, column1Lines - column2Lines),
            _ => new VSpaceCorrection(0, 0)
        };
}
