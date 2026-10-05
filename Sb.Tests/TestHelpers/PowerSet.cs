namespace Sb.Tests.TestHelpers;

/// <summary>
/// Brute-force power-set generation, kept only as a reference oracle for exact-search tests
/// (e.g. Milestone 5.3's subset-sum tests) — not used by Sb.Core's own algorithms.
/// </summary>
internal static class PowerSet
{
    public static IEnumerable<T[]> AllSubsets<T>(this IEnumerable<T> source)
    {
        List<T> list = source.ToList();
        return Enumerable.Range(0, 1 << list.Count)
            .Select(mask => list.Where((_, i) => (mask & (1 << i)) != 0).ToArray());
    }

    public static IEnumerable<T[]> NonEmptySubsets<T>(this IEnumerable<T> source) =>
        source.AllSubsets().Where(s => s.Length >= 1);
}
