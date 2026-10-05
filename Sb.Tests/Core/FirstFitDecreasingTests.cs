using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class FirstFitDecreasingTests
{
    private const int ColumnWidth = 1000; // generous enough that no line ever wraps

    // height = 1 (title) + textLines
    private static Song Make(string title, int height) =>
        Song.Create(title, Enumerable.Range(1, height - 1).Select(i => $"line {i}").ToArray(), 1).Value;

    [Test]
    public void Fill_SongsSumToFullCapacity_MeetsTarget()
    {
        Song a = Make("A", 10);
        Song b = Make("B", 10);

        var result = FirstFitDecreasing.Fill([a, b], capacity: 20, ColumnWidth);

        Assert.That(result.Selected, Is.EqualTo(new[] { a, b }));
        Assert.That(result.TotalHeight, Is.EqualTo(20));
        Assert.That(result.MetTarget, Is.True);
    }

    [Test]
    public void Fill_NotEnoughSongsToReachTarget_DoesNotMeetTarget()
    {
        Song a = Make("A", 5);

        var result = FirstFitDecreasing.Fill([a], capacity: 20, ColumnWidth);

        Assert.That(result.TotalHeight, Is.EqualTo(5));
        Assert.That(result.MetTarget, Is.False);
    }

    [Test]
    public void Fill_SortsLongestFirst_AndSkipsNonFittingToTrySmallerOnes()
    {
        Song small = Make("Small", 3);
        Song medium = Make("Medium", 12);
        Song large = Make("Large", 15);

        // Given out of order on purpose: FFD must sort by height descending itself.
        var result = FirstFitDecreasing.Fill([medium, small, large], capacity: 20, ColumnWidth);

        // Large (15) fits first; medium (12) doesn't (15+12=27>20) and is skipped; small (3) does (15+3=18<=20).
        Assert.That(result.Selected, Is.EqualTo(new[] { large, small }));
        Assert.That(result.TotalHeight, Is.EqualTo(18));
        Assert.That(result.MetTarget, Is.False);
    }

    [Test]
    public void Fill_EmptyCandidates_ReturnsEmptyNotMet()
    {
        var result = FirstFitDecreasing.Fill([], capacity: 20, ColumnWidth);

        Assert.That(result.Selected, Is.Empty);
        Assert.That(result.TotalHeight, Is.EqualTo(0));
        Assert.That(result.MetTarget, Is.False);
    }
}
