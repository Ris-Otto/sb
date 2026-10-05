namespace Sb.Core;

/// <summary>One page of a leaflet chapter: its 1-based number (odd = left-side, FR-1.3.5/FR-1.3.6)
/// and the two columns' fragments.</summary>
public sealed record Page(
    int Number, IReadOnlyList<SongFragment> Column1, IReadOnlyList<SongFragment> Column2, int Column1Lines, int Column2Lines);
