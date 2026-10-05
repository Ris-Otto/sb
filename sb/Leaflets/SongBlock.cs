namespace sb.Leaflets;

/// <summary>
/// One song's own rendered content (title, optional melody attribution, lyric lines) — the
/// atomic unit <see cref="ColumnBalancer"/> packs into columns (research.md §5). Distinct from
/// <see cref="Column"/>, which is the assembled content of a whole physical column and may
/// contain more than one song's <see cref="SongBlock"/>.
/// </summary>
public sealed class SongBlock
{
    public IReadOnlyList<string> Lines { get; }
    public int HeightInLines => Lines.Count;

    public SongBlock(IReadOnlyList<string> lines)
    {
        Lines = lines;
    }
}
