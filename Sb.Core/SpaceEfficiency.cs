namespace Sb.Core;

/// <summary>Shared constants for the space-efficient ordering algorithm (§2.2).</summary>
public static class SpaceEfficiency
{
    /// <summary>The best-fit result aims to fill this fraction of a page's remaining space (FR-1.3.10).</summary>
    public const double FillTarget = 0.95;
}
