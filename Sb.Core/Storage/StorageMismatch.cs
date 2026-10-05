namespace Sb.Core.Storage;

/// <summary>A database record whose recorded path doesn't exist on disk (spec §2.4.3, FR-1.7.5).</summary>
public sealed record StorageMismatch(string Name, string ExpectedPath);
