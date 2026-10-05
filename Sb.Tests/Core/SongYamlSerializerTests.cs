using NUnit.Framework;
using Sb.Core;

namespace Sb.Tests.Core;

[TestFixture]
public class SongYamlSerializerTests
{
    [Test]
    public void RoundTrip_PreservesAllFields()
    {
        var original = Song.Create(
            "Lille Albin",
            ["Första raden", "", "Tredje raden"],
            3,
            writer: "Okänd",
            melody: "Trad.").Value;

        string yaml = SongYamlSerializer.Serialize(original);
        var result = SongYamlSerializer.Deserialize(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Song roundTripped = result.Value;
        Assert.That(roundTripped.Title, Is.EqualTo(original.Title));
        Assert.That(roundTripped.Writer, Is.EqualTo(original.Writer));
        Assert.That(roundTripped.Melody, Is.EqualTo(original.Melody));
        Assert.That(roundTripped.Number, Is.EqualTo(original.Number));
        Assert.That(roundTripped.Text, Is.EqualTo(original.Text));
    }

    [Test]
    public void RoundTrip_OmittedWriterAndMelody_StayNull()
    {
        var original = Song.Create("Lille Albin", ["rad"], 1).Value;

        string yaml = SongYamlSerializer.Serialize(original);
        var result = SongYamlSerializer.Deserialize(yaml);

        Assert.That(result.Value.Writer, Is.Null);
        Assert.That(result.Value.Melody, Is.Null);
    }

    [Test]
    public void Deserialize_FixtureFile_ReturnsExpectedFields()
    {
        string yaml = File.ReadAllText(Path.Combine("TestFiles", "sample-song.yml"));

        var result = SongYamlSerializer.Deserialize(yaml);

        Assert.That(result.IsSuccess, Is.True);
        Song song = result.Value;
        Assert.That(song.Title, Is.EqualTo("Lille Albin"));
        Assert.That(song.Writer, Is.EqualTo("Okänd"));
        Assert.That(song.Melody, Is.EqualTo("Trad."));
        Assert.That(song.Number, Is.EqualTo(1));
        Assert.That(song.Text, Is.EqualTo(new[]
        {
            "Där byggdes en maskot i Norden",
            "Lille Albin det var maskotens namn",
            "Våran vagn!",
            "",
            "Hopp' på Albin, må så vara"
        }));
    }

    [Test]
    public void Deserialize_MalformedYaml_ReturnsStructuredErrorNotException()
    {
        const string malformed = "title: [unterminated";

        var result = SongYamlSerializer.Deserialize(malformed);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
    }

    [Test]
    public void Deserialize_EmptyYaml_ReturnsStructuredError()
    {
        var result = SongYamlSerializer.Deserialize("");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Storage));
    }
}
