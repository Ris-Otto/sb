namespace Sb.Core.Storage;

/// <summary>A chapter's database row (spec §2.4.3 / FR-1.7.3): internal id, owning document id, name,
/// and display title.</summary>
public sealed record ChapterRecord(long Id, long DocumentId, string Name, string Title);
