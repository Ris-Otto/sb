namespace Sb.Core;

/// <summary>
/// The result of simulating a page's two-column fill (FR-1.2.4–FR-1.2.6): which songs landed in
/// each column, how many lines each column used, the unused ("leftover") lines in column 2, and
/// whichever songs didn't fit on this page at all.
/// </summary>
public sealed record PageLayout(
    IReadOnlyList<Song> Column1,
    IReadOnlyList<Song> Column2,
    int Column1Lines,
    int Column2Lines,
    int LeftoverLines,
    IReadOnlyList<Song> RemainingSongs);
