using System.Diagnostics;
using NUnit.Framework;
using Sb.Core;
using Sb.Tests.TestHelpers;

namespace Sb.Tests.Core;

[TestFixture]
public class SubsetSumSearchTests
{
    private const int ColumnWidth = 1000; // generous enough that no line ever wraps

    // height = 2 (title + the space before it, A-12) + text lines
    private static Song Make(string title, int height) =>
        Song.Create(title, Enumerable.Range(1, height - 2).Select(i => $"line {i}").ToArray(), 1).Value;

    [Test]
    public void FindBestFit_MatchesBruteForceOptimum_OnSmallHandCraftedSet()
    {
        Song[] candidates =
        [
            Make("A", 6), Make("B", 5), Make("C", 5), Make("D", 3), Make("E", 7), Make("F", 3)
        ];
        const int capacity = 14;

        int bruteForceOptimum = candidates
            .NonEmptySubsets()
            .Select(subset => subset.Sum(s => SongLayout.Height(s, ColumnWidth)))
            .Where(total => total <= capacity)
            .DefaultIfEmpty(0)
            .Max();

        var result = SubsetSumSearch.FindBestFit(candidates, capacity, ColumnWidth);

        Assert.That(result.TotalHeight, Is.EqualTo(bruteForceOptimum));
        Assert.That(result.Selected.Sum(s => SongLayout.Height(s, ColumnWidth)), Is.EqualTo(result.TotalHeight));
        Assert.That(result.Selected.Distinct().ToArray(), Has.Length.EqualTo(result.Selected.Count), "no song selected twice");
    }

    [Test]
    public void FindBestFit_StrictlyBeatsFfd_WhenFfdIsSuboptimal()
    {
        // Classic FFD bin-covering failure: greedy takes the 6 first, leaving no room for either 5,
        // even though {5, 5} fills the capacity exactly.
        Song a = Make("A", 6);
        Song b = Make("B", 5);
        Song c = Make("C", 5);
        const int capacity = 10;

        var ffd = FirstFitDecreasing.Fill([a, b, c], capacity, ColumnWidth);
        var exact = SubsetSumSearch.FindBestFit([a, b, c], capacity, ColumnWidth);

        Assert.That(ffd.TotalHeight, Is.EqualTo(6));
        Assert.That(ffd.MetTarget, Is.False);

        Assert.That(exact.TotalHeight, Is.EqualTo(10));
        Assert.That(exact.TotalHeight, Is.GreaterThan(ffd.TotalHeight));
        Assert.That(exact.Selected, Is.EquivalentTo(new[] { b, c }));
    }

    [Test]
    public void FindBestFit_TooFewSongsToReachTarget_ReturnsBestAchievable_NoError()
    {
        Song a = Make("A", 3);

        var result = SubsetSumSearch.FindBestFit([a], capacity: 20, ColumnWidth);

        Assert.That(result.TotalHeight, Is.EqualTo(3));
        Assert.That(result.MetTarget, Is.False);
    }

    [Test]
    public void FindBestFit_EmptyCandidates_ReturnsEmptyNoError()
    {
        var result = SubsetSumSearch.FindBestFit([], capacity: 20, ColumnWidth);

        Assert.That(result.Selected, Is.Empty);
        Assert.That(result.TotalHeight, Is.EqualTo(0));
        Assert.That(result.MetTarget, Is.False);
    }

    [Test]
    public void FindBestFit_LargeSyntheticCandidateSet_CompletesQuickly()
    {
        // Regression guard for NFR-2.6.1: this must stay pseudo-polynomial (DP), never factorial/
        // exponential permutation enumeration.
        var candidates = Enumerable.Range(0, 45).Select(i => Make($"Song {i}", height: (i % 12) + 3)).ToArray();
        const int capacity = 200;

        var stopwatch = Stopwatch.StartNew();
        var result = SubsetSumSearch.FindBestFit(candidates, capacity, ColumnWidth);
        stopwatch.Stop();

        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000),
            "exact search took too long — check it's still DP-based, not permutation enumeration");
        Assert.That(result.TotalHeight, Is.LessThanOrEqualTo(capacity));
        Assert.That(result.TotalHeight, Is.GreaterThan(0));
    }
}
