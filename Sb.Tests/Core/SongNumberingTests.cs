using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class SongNumberingTests
{
    private static Song Make(string title, int number) => Song.Create(title, ["x"], number).Value;

    private static IReadOnlyList<Song> ThreeSongs() =>
        [Make("A", 1), Make("B", 2), Make("C", 3)];

    private static void AssertSequence(IReadOnlyList<Song> songs, params (string Title, int Number)[] expected)
    {
        Assert.That(songs.Select(s => (s.Title, s.Number)), Is.EqualTo(expected));
    }

    [Test]
    public void InsertAt_Start_ShiftsAllOthersUp()
    {
        var result = SongNumbering.InsertAt(ThreeSongs(), 1, Make("New", 1));

        AssertSequence(result, ("New", 1), ("A", 2), ("B", 3), ("C", 4));
    }

    [Test]
    public void InsertAt_Middle_ShiftsLaterOnesUp()
    {
        var result = SongNumbering.InsertAt(ThreeSongs(), 2, Make("New", 1));

        AssertSequence(result, ("A", 1), ("New", 2), ("B", 3), ("C", 4));
    }

    [Test]
    public void InsertAt_End_AppendsWithNextNumber()
    {
        var songs = ThreeSongs();
        var result = SongNumbering.InsertAt(songs, songs.Count + 1, Make("New", 1));

        AssertSequence(result, ("A", 1), ("B", 2), ("C", 3), ("New", 4));
    }

    [Test]
    public void Append_NoExplicitPosition_GetsNextSequentialNumber()
    {
        var result = SongNumbering.Append(ThreeSongs(), Make("New", 1));

        AssertSequence(result, ("A", 1), ("B", 2), ("C", 3), ("New", 4));
    }

    [Test]
    public void Append_OnEmptyList_GetsNumberOne()
    {
        var result = SongNumbering.Append([], Make("First", 1));

        AssertSequence(result, ("First", 1));
    }

    [Test]
    public void RemoveAt_Start_ShiftsAllOthersDown()
    {
        var result = SongNumbering.RemoveAt(ThreeSongs(), 1);

        AssertSequence(result, ("B", 1), ("C", 2));
    }

    [Test]
    public void RemoveAt_Middle_ShiftsLaterOnesDown()
    {
        var result = SongNumbering.RemoveAt(ThreeSongs(), 2);

        AssertSequence(result, ("A", 1), ("C", 2));
    }

    [Test]
    public void RemoveAt_End_NoShiftNeeded()
    {
        var result = SongNumbering.RemoveAt(ThreeSongs(), 3);

        AssertSequence(result, ("A", 1), ("B", 2));
    }

    [Test]
    public void RemoveAt_OnlyElement_ResultsInEmptyList_NoThrow()
    {
        IReadOnlyList<Song> single = [Make("Only", 1)];

        var result = SongNumbering.RemoveAt(single, 1);

        Assert.That(result, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void InsertAt_OutOfRangePosition_Throws(int position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SongNumbering.InsertAt(ThreeSongs(), position, Make("New", 1)));
    }

    [TestCase(0)]
    [TestCase(4)]
    public void RemoveAt_OutOfRangePosition_Throws(int position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SongNumbering.RemoveAt(ThreeSongs(), position));
    }
}
