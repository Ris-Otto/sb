namespace sb.Songs;

/// <summary>
/// A song's single on-disk `.yml` file (`sb/specification.md` §2.5.2) — the source of truth for
/// all of a song's data, including its singing-order number (`Number`); the `songs` index row's
/// `singing_order_number` column is only a fast-lookup mirror of it (research.md §12).
/// </summary>
internal sealed class SongFile
{
    public string Title { get; set; } = "";
    public string? Writer { get; set; }
    public string? Melody { get; set; }
    public int Number { get; set; }
    public List<string> Text { get; set; } = new();
    public List<string>? Score { get; set; }
}
