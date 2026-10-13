namespace Sb.Core;

/// <summary>Shared constants for the space-efficient ordering algorithm (§2.2).</summary>
public static class SpaceEfficiency
{
    /// <summary>The best-fit result aims to fill this fraction of a page's two columns combined,
    /// measured in lines actually placed (FR-1.3.10, FR-1.3.18). The per-capacity searches
    /// (<see cref="FirstFitDecreasing"/>, <see cref="SubsetSumSearch"/>) also use it, against the
    /// capacity they are given.</summary>
    public const double FillTarget = 0.95;
}
