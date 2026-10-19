namespace sb.Leaflets;

/// <summary>
/// One song's own rendered content (title, optional melody attribution, lyric lines) — the
/// atomic unit <see cref="ColumnBalancer"/> packs into columns (research.md §5). Distinct from
/// <see cref="Column"/>, which is the assembled content of a whole physical column and may
/// contain more than one song's <see cref="SongBlock"/>.
/// </summary>
public sealed class SongBlock
{
    /// <summary>One visually blank line that is a real line break, valid under `\raggedright`
    /// (a bare `\` is not: it ends the paragraph — "There's no line here to end").</summary>
    public const string BlankLine = "\\mbox{}\\\\";

    public IReadOnlyList<string> Lines { get; }
    public int HeightInLines => Lines.Count;

    /// <summary>The blank line above this song's title (FR-027). Kept outside <see cref="Lines"/>:
    /// <see cref="ColumnBalancer"/> adds it only when the song is not first in its column.</summary>
    public int LeadingBlankLines => 1;

    public SongBlock(IReadOnlyList<string> lines)
    {
        Lines = lines;
    }
}
