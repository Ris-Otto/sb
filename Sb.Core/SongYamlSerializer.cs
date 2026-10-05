using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sb.Core;

/// <summary>(De)serializes a <see cref="Song"/> to/from the `.yml` shape in spec §2.4.2.</summary>
public static class SongYamlSerializer
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(LowerCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(LowerCaseNamingConvention.Instance)
        .Build();

    public static string Serialize(Song song)
    {
        var dto = new SongYamlDto
        {
            Title = song.Title,
            Writer = song.Writer,
            Melody = song.Melody,
            Number = song.Number,
            Text = song.Text.ToList()
        };
        return Serializer.Serialize(dto);
    }

    public static Result<Song> Deserialize(string yaml)
    {
        SongYamlDto? dto;
        try
        {
            dto = Deserializer.Deserialize<SongYamlDto>(yaml);
        }
        catch (YamlException ex)
        {
            return Result<Song>.Failure(new Error(ErrorKind.Storage, $"Malformed song YAML: {ex.Message}"));
        }

        if (dto is null)
            return Result<Song>.Failure(new Error(ErrorKind.Storage, "Song YAML is empty."));

        return Song.Create(dto.Title ?? string.Empty, dto.Text ?? [], dto.Number, dto.Writer, dto.Melody);
    }
}
