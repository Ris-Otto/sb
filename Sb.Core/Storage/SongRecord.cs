namespace Sb.Core.Storage;

/// <summary>A song's database row (spec §2.4.3 / FR-1.7.4): internal id, owning chapter id, title, and number.</summary>
public sealed record SongRecord(long Id, long ChapterId, string Title, int Number);
