namespace Sb.Core;

/// <summary>The outcome of an exact subset-sum/bounded-knapsack search (FR-1.3.11): the chosen
/// songs, their total height, and whether the 95% fill target (FR-1.3.10) was met.</summary>
public sealed record SubsetSumResult(IReadOnlyList<Song> Selected, int TotalHeight, bool MetTarget);
