namespace sb.Index;

/// <summary>
/// Produced when an indexed location doesn't match the filesystem (FR-023), or — for
/// <see cref="MismatchEntityKind.SongNumber"/> — when a song's file content disagrees with the
/// index's mirrored singing-order number (research.md §12), following the same "surface it,
/// never auto-repair" pattern.
/// </summary>
public sealed class MismatchReport
{
    public MismatchEntityKind EntityKind { get; }
    public int Id { get; }
    public string ExpectedPath { get; }
    public string? Detail { get; }

    public MismatchReport(MismatchEntityKind entityKind, int id, string expectedPath, string? detail = null)
    {
        EntityKind = entityKind;
        Id = id;
        ExpectedPath = expectedPath;
        Detail = detail;
    }

    public override string ToString() => EntityKind == MismatchEntityKind.SongNumber
        ? $"Song #{Id}: {Detail}"
        : $"{EntityKind} #{Id}: expected to find '{ExpectedPath}', but it is missing.";
}
