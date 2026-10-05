using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class SongTests
{
    private static readonly string[] SampleText = ["Första raden", "Andra raden"];

    [Test]
    public void Create_WithTitleAndText_Succeeds()
    {
        var result = Song.Create("Lille Albin", SampleText, 1, writer: "Okänd", melody: "Trad.");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Title, Is.EqualTo("Lille Albin"));
        Assert.That(result.Value.Writer, Is.EqualTo("Okänd"));
        Assert.That(result.Value.Melody, Is.EqualTo("Trad."));
        Assert.That(result.Value.Number, Is.EqualTo(1));
        Assert.That(result.Value.Text, Is.EqualTo(SampleText));
    }

    [Test]
    public void Create_WriterAndMelodyOmitted_AreNull()
    {
        var result = Song.Create("Lille Albin", SampleText, 1);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Writer, Is.Null);
        Assert.That(result.Value.Melody, Is.Null);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_BlankTitle_Fails(string title)
    {
        var result = Song.Create(title, SampleText, 1);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Create_EmptyText_Fails()
    {
        var result = Song.Create("Lille Albin", [], 1);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void Create_NullText_Fails()
    {
        var result = Song.Create("Lille Albin", null!, 1);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Create_NonPositiveNumber_Fails(int number)
    {
        var result = Song.Create("Lille Albin", SampleText, number);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Validation));
    }

    [Test]
    public void WithNumber_ReturnsNewInstance_OnlyNumberChanges()
    {
        var song = Song.Create("Lille Albin", SampleText, 1, writer: "Okänd", melody: "Trad.").Value;

        var renumbered = song.WithNumber(5);

        Assert.That(renumbered, Is.Not.SameAs(song));
        Assert.That(renumbered.Number, Is.EqualTo(5));
        Assert.That(renumbered.Title, Is.EqualTo(song.Title));
        Assert.That(renumbered.Writer, Is.EqualTo(song.Writer));
        Assert.That(renumbered.Melody, Is.EqualTo(song.Melody));
        Assert.That(renumbered.Text, Is.EqualTo(song.Text));
        Assert.That(song.Number, Is.EqualTo(1), "original must stay unchanged (immutability)");
    }
}
