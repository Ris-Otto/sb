namespace Sb.Core;

/// <summary>
/// Exact subset-sum / bounded-knapsack search (FR-1.3.11): finds the subset of candidate songs
/// whose total height best fills a capacity, without exceeding it. Runs as a pseudo-polynomial
/// `O(songs × capacity)` dynamic program over quantized line-height units — not permutation
/// enumeration (NFR-2.6.1) — so it stays fast even for realistic song counts.
/// </summary>
public static class SubsetSumSearch
{
    public static SubsetSumResult FindBestFit(IReadOnlyList<Song> candidates, int capacity, int columnWidth)
    {
        if (capacity <= 0 || candidates.Count == 0)
            return new SubsetSumResult([], 0, false);

        int[] heights = candidates.Select(song => SongLayout.Height(song, columnWidth)).ToArray();

        // dp[c] = best achievable total height using items considered so far, within budget c.
        var dp = new int[capacity + 1];
        var taken = new bool[candidates.Count, capacity + 1];

        for (int i = 0; i < candidates.Count; i++)
        {
            int height = heights[i];
            for (int c = capacity; c >= height; c--)
            {
                int candidateTotal = dp[c - height] + height;
                if (candidateTotal > dp[c])
                {
                    dp[c] = candidateTotal;
                    taken[i, c] = true;
                }
            }
        }

        var selected = new List<Song>();
        int remainingCapacity = capacity;
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (taken[i, remainingCapacity])
            {
                selected.Add(candidates[i]);
                remainingCapacity -= heights[i];
            }
        }
        selected.Reverse();

        int totalHeight = dp[capacity];
        bool metTarget = totalHeight >= capacity * SpaceEfficiency.FillTarget;
        return new SubsetSumResult(selected, totalHeight, metTarget);
    }
}
