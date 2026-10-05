namespace Sb.Core;

/// <summary>
/// A contiguous slice of a song placed in one column: either the whole song, or a piece of one
/// when it's split across a column/page boundary (FR-1.3.4, FR-1.3.5). <see cref="IncludesTitle"/>
/// is true only for the fragment that starts the song (the one carrying its `\paragraph*` title
/// line) — a continuation fragment is pure text, since the title isn't re-rendered.
///
/// <see cref="ContinuesOnNextPage"/>/<see cref="ContinuesFromPreviousPage"/> distinguish a
/// page-boundary split (FR-1.3.5, FR-1.3.15 — needs a reader-facing continuation notice,
/// FR-1.3.16) from a same-page column 1→2 split (FR-1.3.4 — needs no notice, both halves are
/// visible at once). Both default to false for a column-boundary split or a fragment that hasn't
/// been split at all.
/// </summary>
public sealed record SongFragment(
    Song Song,
    bool IncludesTitle,
    IReadOnlyList<string> Lines,
    int Height,
    bool ContinuesOnNextPage = false,
    bool ContinuesFromPreviousPage = false)
{
    public static SongFragment Whole(Song song, int columnWidth) =>
        new(song, true, song.Text, SongLayout.Height(song, columnWidth));
}
