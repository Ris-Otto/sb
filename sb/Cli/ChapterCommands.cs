using System.Text;
using sb.Chapters;
using sb.Songs;

namespace sb.Cli;

/// <summary>Logic behind `sb chapters add|remove|list` (contracts/cli-commands.md).</summary>
public static class ChapterCommands
{
    public static Chapter Add(ChapterStore store, string documentName, string? name = null, string? title = null)
    {
        var resolvedName = name ?? $"{documentName}_chp_{store.List(documentName).Count + 1}";
        return store.Add(documentName, resolvedName, title);
    }

    public static void Remove(ChapterStore store, string documentName, IEnumerable<string> names) =>
        store.Remove(documentName, names);

    public static string List(ChapterStore chapters, string documentName, bool includeSongs = false, SongStore? songs = null)
    {
        var output = new StringBuilder();

        foreach (var chapter in chapters.List(documentName))
        {
            output.AppendLine(chapter.Name);
            if (includeSongs && songs is not null)
            {
                foreach (var song in songs.List(documentName, chapter.Name))
                    output.AppendLine($"  {song.SingingOrderNumber}. {song.Title}");
            }
        }

        return output.ToString();
    }
}
