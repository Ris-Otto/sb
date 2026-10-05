namespace Sb.Core.Storage;

/// <summary>A document's database row (spec §2.4.3 / FR-1.7.2): internal id, filename, and the path
/// to its main folder.</summary>
public sealed record DocumentRecord(long Id, string Filename, string Path);
