using System.Collections.Generic;
using static System.Linq.Enumerable;

namespace sb;

internal static class PermutationsAndCombinations
{
    
    public static long nCr(int n, int r)
    {
        // naive: return Factorial(n) / (Factorial(r) * Factorial(n - r));
        return nPr(n, r) / Factorial(r);
    }

    public static long nPr(int n, int r)
    {
        // naive: return Factorial(n) / Factorial(n - r);
        return FactorialDivision(n, n - r);
    }

    private static long FactorialDivision(int topFactorial, int divisorFactorial)
    {
        long result = 1;
        for (int i = topFactorial; i > divisorFactorial; i--)
            result *= i;
        return result;
    }

    private static long Factorial(int i)
    {
        if (i <= 1)
            return 1;
        return i * Factorial(i - 1);
    }

    public static IEnumerable<T[]> CreateCombinationsList<T>(this IEnumerable<T> TList) {
        List<T> enumerable = TList.ToList();
        return Range(0, 1 << (enumerable.Count))
            .Select(index => enumerable
                .Where((v, i) => (index & (1 << i)) != 0)
                .ToArray());
    }
    
    public static IEnumerable<T[]> CreateRelevantCombinationsList<T>(this IEnumerable<T> TList) {
        return TList.CreateCombinationsList().Where(s => s.Length >= 1);
    }

}