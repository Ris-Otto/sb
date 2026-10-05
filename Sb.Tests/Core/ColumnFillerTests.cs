using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class ColumnFillerTests
{
    // One title line + N text lines, with columnWidth generous enough that no wrapping occurs,
    // so each song's height is exactly (1 + N) and tests can reason about exact line counts.
    private const int ColumnWidth = 1000;

    private static Song Make(string title, int textLines, int number = 1) =>
        Song.Create(title, Enumerable.Range(1, textLines).Select(i => $"line {i}").ToArray(), number).Value;

    [Test]
    public void Fill_WholeSongsExactlyFillColumn1_NoOverflow()
    {
        Song a = Make("A", textLines: 4); // height 5
        Song b = Make("B", textLines: 4); // height 5

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a, b }));
        Assert.That(layout.Column1Lines, Is.EqualTo(10));
        Assert.That(layout.Column2, Is.Empty);
        Assert.That(layout.LeftoverLines, Is.EqualTo(10));
        Assert.That(layout.RemainingSongs, Is.Empty);
    }

    [Test]
    public void Fill_SongOverflowsFromColumn1ToColumn2()
    {
        Song a = Make("A", textLines: 5); // height 6
        Song b = Make("B", textLines: 6); // height 7 — doesn't fit in the remaining 4 lines of column 1

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a, b }), "b starts in column 1");
        Assert.That(layout.Column1Lines, Is.EqualTo(10));
        Assert.That(layout.Column2, Is.EqualTo(new[] { b }), "b continues into column 2");
        Assert.That(layout.Column2Lines, Is.EqualTo(3));
        Assert.That(layout.LeftoverLines, Is.EqualTo(7));
        Assert.That(layout.RemainingSongs, Is.Empty);
    }

    [Test]
    public void Fill_LeftoverReportedWhenSongsRunOut()
    {
        Song a = Make("A", textLines: 9); // height 10, fills column 1 exactly
        Song b = Make("B", textLines: 3); // height 4, placed whole in column 2

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column2, Is.EqualTo(new[] { b }));
        Assert.That(layout.Column2Lines, Is.EqualTo(4));
        Assert.That(layout.LeftoverLines, Is.EqualTo(6));
    }

    [Test]
    public void Fill_NextSongAfterColumn1ExactlyFull_PlacedWholeInColumn2_NotTreatedAsOverflow()
    {
        Song a = Make("A", textLines: 9); // height 10, exactly fills column 1 (remainingColumn1 becomes 0)
        Song b = Make("B", textLines: 2); // height 3, should land whole in column 2, not split

        var layout = ColumnFiller.Fill([a, b], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a }));
        Assert.That(layout.Column2, Is.EqualTo(new[] { b }));
        Assert.That(layout.Column2Lines, Is.EqualTo(3));
    }

    [Test]
    public void Fill_SongsLeftOverAfterPageIsFull_AreReturnedAsRemaining()
    {
        Song a = Make("A", textLines: 9); // height 10, fills column 1
        Song b = Make("B", textLines: 9); // height 10, fills column 2 exactly (no room to overflow into)
        Song c = Make("C", textLines: 4); // height 5, doesn't fit anywhere on this page

        var layout = ColumnFiller.Fill([a, b, c], linesPerColumn: 10, ColumnWidth);

        Assert.That(layout.Column1, Is.EqualTo(new[] { a }));
        Assert.That(layout.Column2, Is.EqualTo(new[] { b }));
        Assert.That(layout.LeftoverLines, Is.EqualTo(0));
        Assert.That(layout.RemainingSongs, Is.EqualTo(new[] { c }));
    }
}
