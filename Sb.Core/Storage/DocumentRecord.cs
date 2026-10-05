namespace Sb.Core.Storage;

/// <summary>A document's database row (spec §2.4.3 / FR-1.7.2): internal id, filename, the path to
/// its main folder, and its display title/subtitle (persisted so `documents build` can regenerate
/// the main `.tex` from the database alone).</summary>
public sealed record DocumentRecord(long Id, string Filename, string Path, string Title, string? Subtitle);
