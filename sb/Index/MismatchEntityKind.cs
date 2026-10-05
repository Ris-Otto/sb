namespace sb.Index;

public enum MismatchEntityKind
{
    Document,
    Chapter,
    Song,

    /// <summary>
    /// A song's file exists, but its `number` field disagrees with the index's mirrored
    /// `singing_order_number` (research.md §12) — a content mismatch, not a missing-path one.
    /// </summary>
    SongNumber
}
