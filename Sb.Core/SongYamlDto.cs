namespace Sb.Core;

/// <summary>Plain mutable shape YamlDotNet (de)serializes to/from — kept separate from the
/// validated, immutable <see cref="Song"/> domain model.</summary>
internal sealed class SongYamlDto
{
    public string? Title { get; set; }
    public string? Writer { get; set; }
    public string? Melody { get; set; }
    public int Number { get; set; }
    public List<string>? Text { get; set; }
}
