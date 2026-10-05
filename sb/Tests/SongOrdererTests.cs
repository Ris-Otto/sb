using NUnit.Framework;
using sb.Leaflets;
using sb.Ordering;
using sb.Songs;

namespace sb.Tests;

[TestFixture]
public class SongOrdererTests
{
    private static PageMetrics SmallPageMetrics(int linesPerPage) =>
        new(paperHeightPt: linesPerPage * 10, topMarginPt: 0, bottomMarginPt: 0, baselineSkipPt: 10);

    [Test]
    public void Order_AsListed_PreservesExactInputOrder()
    {
        var songs = new[]
        {
            new Song("First", "text one", 1),
            new Song("Second", "text two", 2),
            new Song("Third", "text three", 3)
        };

        var ordered = SongOrderer.Order(songs, OrderingMode.AsListed, SmallPageMetrics(10));

        Assert.That(ordered.Select(s => s.Title), Is.EqualTo(new[] { "First", "Second", "Third" }));
    }

    [Test]
    public void Order_AsListed_ReturnsAllSongsExactlyOnce()
    {
        var songs = new[]
        {
            new Song("A", "a", 1),
            new Song("B", "b", 2)
        };

        var ordered = SongOrderer.Order(songs, OrderingMode.AsListed, SmallPageMetrics(10));

        Assert.That(ordered, Has.Count.EqualTo(2));
        Assert.That(ordered, Is.EquivalentTo(songs));
    }

    private static Song MakeSong(string title, int lineCount) =>
        new(title, string.Join("\n", Enumerable.Repeat("line", lineCount)), singingOrderNumber: 1);

    [Test]
    public void Order_SpaceEfficient_MinimizesRealUnusedColumnSpace_WithinTheExactSearchBound()
    {
        // 4 songs => 4! = 24 permutations, comfortably within the default 100-permutation bound.
        // Heights (title + lines): A=52, B=2, C=52, D=2. LinesPerPage=53 => 2 columns hold 106,
        // but the total (108) can't fit in 2 columns, so 3 is the true minimum — achievable only
        // if B and D (the two small songs) end up adjacent, sharing one column. As-listed order
        // [A,B,C,D] does NOT achieve this (A+B=54>53, so B starts its own column; C+D likewise
        // needs a 4th column) — the space-efficient search must find a 3-column arrangement.
        var a = MakeSong("A", 51); // 1 + 51 = 52
        var b = MakeSong("B", 1);  // 1 + 1 = 2
        var c = MakeSong("C", 51); // 52
        var d = MakeSong("D", 1);  // 2
        var songs = new[] { a, b, c, d };
        var metrics = SmallPageMetrics(53);

        var ordered = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics);

        var titles = ordered.Select(s => s.Title).ToList();
        Assert.That(titles, Is.Not.EqualTo(new[] { "A", "B", "C", "D" }));
        // Whatever order was chosen, B and D must be adjacent (the only way to reach 3 columns).
        var bIndex = titles.IndexOf("B");
        var dIndex = titles.IndexOf("D");
        Assert.That(Math.Abs(bIndex - dIndex), Is.EqualTo(1));
    }

    [Test]
    public void Order_SpaceEfficient_Greedy_PreservesOriginalOrder_WhenEverythingAlreadyFitsOneColumn()
    {
        // 6 songs => 6! = 720 permutations, exceeding maxPermutations: 1, forcing the greedy
        // fallback (research.md §4, revised 2026-09-28). Total height (8+2+6+6+9+3=34) fits
        // entirely within one 53-line column, so the greedy heuristic never needs to deviate from
        // singing order at all — a direct requirement (spec.md FR-007): `space-efficient` mode
        // should still track singing order as closely as possible.
        var songs = new[]
        {
            MakeSong("A", 7), // height 8
            MakeSong("B", 1), // height 2
            MakeSong("C", 5), // height 6
            MakeSong("D", 5), // height 6
            MakeSong("E", 8), // height 9
            MakeSong("F", 2)  // height 3
        };

        var ordered = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, SmallPageMetrics(53), maxPermutations: 1);

        Assert.That(ordered.Select(s => s.Title), Is.EqualTo(new[] { "A", "B", "C", "D", "E", "F" }));
    }

    [Test]
    public void Order_SpaceEfficient_Greedy_FindsTheSameSpaceSavingArrangementAsExactSearch_ViaSimulation()
    {
        // Same data/expectation as Order_SpaceEfficient_MinimizesRealUnusedColumnSpace_WithinThe
        // ExactSearchBound above, but forced through GreedyFallback (maxPermutations: 1) instead
        // of ExactSearch (research.md §4, revised 2026-09-28, second correction). As-listed order
        // [A,B,C,D] wastes a whole 4th column; the true minimum (3 columns) is only reachable by
        // pairing B and D together. GreedyFallback now evaluates each placement by actually
        // simulating ColumnBalancer.Fill (like ExactSearch already does) rather than an
        // FR-025-unaware approximate capacity model, so it finds this same real improvement.
        var a = MakeSong("A", 51); // height 52
        var b = MakeSong("B", 1);  // height 2
        var c = MakeSong("C", 51); // height 52
        var d = MakeSong("D", 1);  // height 2
        var songs = new[] { a, b, c, d };
        var metrics = SmallPageMetrics(53);

        var ordered = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics, maxPermutations: 1);

        var titles = ordered.Select(s => s.Title).ToList();
        Assert.That(titles, Is.Not.EqualTo(new[] { "A", "B", "C", "D" }));
        var bIndex = titles.IndexOf("B");
        var dIndex = titles.IndexOf("D");
        Assert.That(Math.Abs(bIndex - dIndex), Is.EqualTo(1));
    }

    [Test]
    public void Order_SpaceEfficient_IsDeterministic_AcrossRepeatedCalls()
    {
        var songs = new[] { MakeSong("X", 3), MakeSong("Y", 3), MakeSong("Z", 3) };
        var metrics = SmallPageMetrics(10);

        var first = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics);
        var second = SongOrderer.Order(songs, OrderingMode.SpaceEfficient, metrics);

        Assert.That(first.Select(s => s.Title), Is.EqualTo(second.Select(s => s.Title)));
    }
}
