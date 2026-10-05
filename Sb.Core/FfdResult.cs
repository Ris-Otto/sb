namespace Sb.Core;

/// <summary>The outcome of a First-Fit-Decreasing pass (FR-1.3.12): the songs it chose, their
/// total height, and whether the 95% fill target (FR-1.3.10) was met.</summary>
public sealed record FfdResult(IReadOnlyList<Song> Selected, int TotalHeight, bool MetTarget);
