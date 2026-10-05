namespace Sb.Core;

/// <summary>
/// The result of simulating a page's two-column fill (FR-1.2.4–FR-1.2.6): which song fragments
/// landed in each column (a split song's two pieces share the same <see cref="SongFragment.Song"/>
/// reference), how many lines each column used, the unused ("leftover") lines in column 2, and
/// whichever fragments didn't fit on this page at all.
/// </summary>
public sealed record PageLayout(
    IReadOnlyList<SongFragment> Column1,
    IReadOnlyList<SongFragment> Column2,
    int Column1Lines,
    int Column2Lines,
    int LeftoverLines,
    IReadOnlyList<SongFragment> RemainingFragments);
