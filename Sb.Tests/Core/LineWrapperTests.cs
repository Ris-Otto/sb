using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class LineWrapperTests
{
    [Test]
    public void CountOutputLines_ShortLine_ReturnsOne()
    {
        Assert.That(LineWrapper.CountOutputLines("Våran vagn!", columnWidth: 30), Is.EqualTo(1));
    }

    [Test]
    public void CountOutputLines_EmptyLine_ReturnsOne()
    {
        // A blank line (verse separator) is still one output line — FR-1.2.8: one input line, one output line.
        Assert.That(LineWrapper.CountOutputLines("", columnWidth: 30), Is.EqualTo(1));
    }

    [Test]
    public void CountOutputLines_LineWiderThanColumn_WrapsToExpectedCount()
    {
        string line = "Där byggdes en maskot i Norden";
        Assert.That(line.Length, Is.EqualTo(30));

        Assert.That(LineWrapper.CountOutputLines(line, columnWidth: 10), Is.EqualTo(4));
    }

    [Test]
    public void Wrap_ShortLine_ReturnsLineUnchanged()
    {
        string line = "Våran vagn!";
        Assert.That(LineWrapper.Wrap(line, columnWidth: 30), Is.EqualTo(new[] { line }));
    }

    [Test]
    public void Wrap_LineWiderThanColumn_NoLineExceedsColumnWidth()
    {
        string line = "Där byggdes en maskot i Norden";

        var wrapped = LineWrapper.Wrap(line, columnWidth: 10);

        Assert.That(wrapped, Has.All.Matches<string>(l => l.Length <= 10));
    }

    [Test]
    public void Wrap_LineWiderThanColumn_PreservesEveryWord_NoTruncationOrDrops()
    {
        string line = "Där byggdes en maskot i Norden";

        var wrapped = LineWrapper.Wrap(line, columnWidth: 10);

        string[] originalWords = line.Split(' ');
        string[] reassembledWords = string.Join(' ', wrapped).Split(' ');
        Assert.That(reassembledWords, Is.EqualTo(originalWords));
    }

    [Test]
    public void Wrap_WordLongerThanColumnWidth_KeptWholeOnItsOwnLine()
    {
        string line = "supercalifragilisticexpialidocious";

        var wrapped = LineWrapper.Wrap(line, columnWidth: 10);

        Assert.That(wrapped, Has.Exactly(1).EqualTo(line));
    }

    [Test]
    public void Wrap_LeadingSpace_IsPreserved()
    {
        Assert.That(LineWrapper.Wrap(" hello", columnWidth: 30), Is.EqualTo(new[] { " hello" }));
    }

    [Test]
    public void Wrap_MultipleLeadingSpaces_AreAllPreserved()
    {
        Assert.That(LineWrapper.Wrap("   hello", columnWidth: 30), Is.EqualTo(new[] { "   hello" }));
    }

    [Test]
    public void Wrap_RealLyricsFixture_NoWordsLostAcrossWholeSong()
    {
        string[] text = File.ReadAllLines(Path.Combine("TestFiles", "Lille_albin.txt"));

        foreach (string line in text)
        {
            var wrapped = LineWrapper.Wrap(line, columnWidth: 20);

            string[] originalWords = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] reassembledWords = string.Join(' ', wrapped).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(reassembledWords, Is.EqualTo(originalWords), $"word mismatch for line: '{line}'");
        }
    }

    [Test]
    public void CountOutputLines_ForSongText_SumsEachLineIndependently()
    {
        string[] text =
        [
            "Där byggdes en maskot i Norden",
            "Lille Albin det var maskotens namn",
            "Våran vagn!"
        ];

        int total = LineWrapper.CountOutputLines(text, columnWidth: 10);

        int expected = text.Sum(line => LineWrapper.CountOutputLines(line, columnWidth: 10));
        Assert.That(total, Is.EqualTo(expected));
        Assert.That(total, Is.GreaterThan(text.Length), "at least one line must have actually wrapped");
    }
}
