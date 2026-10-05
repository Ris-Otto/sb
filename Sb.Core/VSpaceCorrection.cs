namespace Sb.Core;

/// <summary>The `\vspace` (in line units) to append to each column so both end at the same height
/// (FR-1.2.7). Exactly one of the two is non-zero, unless the columns are already equal.</summary>
public readonly record struct VSpaceCorrection(int Column1VSpace, int Column2VSpace);
