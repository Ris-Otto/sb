namespace Sb.Core.Storage;

/// <summary>
/// Detects drift between the database and the filesystem (FR-1.7.5) — expected to arise only from
/// changes made outside of SB, since SB's own operations are atomic (see <see cref="AtomicOperation"/>).
/// </summary>
public static class MismatchDetector
{
    public static IReadOnlyList<StorageMismatch> FindDocumentMismatches(IEnumerable<DocumentRecord> documents) =>
        documents
            .Where(d => !Directory.Exists(d.Path))
            .Select(d => new StorageMismatch(d.Filename, d.Path))
            .ToList();
}
