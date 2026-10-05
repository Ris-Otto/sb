using System.Text;
using sb.Formatting;
using sb.Songs;

namespace sb.Cli;

/// <summary>Logic behind `sb songs add|remove|list|modify` (contracts/cli-commands.md).</summary>
public static class SongCommands
{
    /// <summary>
    /// <paramref name="text"/> is either literal lyric text, or a path to an existing file whose
    /// contents are read and used as the lyric text (2026-09-26 Clarification) — a convenience so
    /// callers don't need a shell workaround (e.g. PowerShell's `Get-Content -Raw`) to pass a
    /// song's text from a file.
    /// </summary>
    public static Song Add(
        SongStore store,
        string documentName,
        string chapterName,
        string title,
        string text,
        int singingOrderNumber,
        string? writer = null,
        string? melody = null) =>
        store.Add(documentName, chapterName, title, ResolveText(text), singingOrderNumber, writer, melody);

    /// <summary>
    /// The existence check uses a Unicode-normalized (NFC) copy of <paramref name="text"/>
    /// (003-utf8-encoding, research.md §4), guarding against a normalization-form mismatch
    /// between how the value was typed/pasted and how the file's name is actually stored on
    /// disk. The literal-text fallback always returns the original, un-normalized
    /// <paramref name="text"/> — never the normalized path — so a value that isn't a file path is
    /// stored exactly as the user typed it.
    /// </summary>
    private static string ResolveText(string text)
    {
        var normalizedPath = PathText.NormalizeForFileSystem(text);
        return File.Exists(normalizedPath) ? File.ReadAllText(normalizedPath, Encoding.UTF8) : text;
    }

    public static void Remove(SongStore store, string documentName, string songName) =>
        store.Remove(documentName, songName);

    public static string List(SongStore store, string documentName, string? chapterName = null)
    {
        var output = new StringBuilder();
        foreach (var song in store.List(documentName, chapterName))
            output.AppendLine($"{song.SingingOrderNumber}. {song.Title}");
        return output.ToString();
    }

    public static void Modify(
        SongStore store,
        string documentName,
        string songName,
        string? title = null,
        string? writer = null,
        string? melody = null,
        string? chapter = null,
        int? singingOrderNumber = null) =>
        store.Modify(documentName, songName, title, writer, melody, chapter, singingOrderNumber);
}
