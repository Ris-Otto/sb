using System.Text;
using sb.Chapters;
using sb.Documents;
using sb.Formatting;
using sb.Index;
using sb.Leaflets;
using sb.Ordering;
using sb.Songs;

namespace sb.Cli;

/// <summary>
/// Logic behind `sb documents create|list|delete|rename|build` (contracts/cli-commands.md).
/// Kept independent of the System.CommandLine wiring in <c>Program.cs</c> so it can be unit
/// tested directly, without going through argument parsing.
/// </summary>
public static class DocumentCommands
{
    public static Document Create(
        DocumentStore store,
        string name,
        string title,
        string? path = null,
        string? subtitle = null)
    {
        var resolvedPath = path ?? Path.Combine(Directory.GetCurrentDirectory(), name);
        return store.Create(name, title, resolvedPath, subtitle);
    }

    public static string List(
        DocumentStore documents,
        LocationMismatchDetector detector,
        bool includeChapters = false,
        bool includeSongs = false,
        ChapterStore? chapters = null,
        SongStore? songs = null)
    {
        var output = new StringBuilder();
        var allMismatches = detector.DetectAll();
        var mismatchedPaths = allMismatches.Select(m => m.ExpectedPath).ToList();

        foreach (var document in documents.List())
        {
            // A document whose own location (or anything under it) is mismatched may not have
            // readable chapter/song files — skip nesting for it rather than risk an unhandled
            // I/O error; its MISMATCH line below still tells the user something is wrong (FR-023).
            var isMismatched = mismatchedPaths.Any(p => p.StartsWith(document.Path, StringComparison.Ordinal));

            output.AppendLine(isMismatched
                ? $"{document.Name} — {document.Title} [LOCATION MISMATCH — see below]"
                : $"{document.Name} — {document.Title}");

            if (isMismatched)
                continue;

            if (includeChapters && chapters is not null)
            {
                foreach (var chapter in chapters.List(document.Name))
                {
                    output.AppendLine($"  {chapter.Name}");
                    if (includeSongs && songs is not null)
                    {
                        foreach (var song in songs.List(document.Name, chapter.Name))
                            output.AppendLine($"    {song.SingingOrderNumber}. {song.Title}");
                    }
                }
            }
            else if (includeSongs && songs is not null)
            {
                foreach (var song in songs.List(document.Name))
                    output.AppendLine($"  {song.SingingOrderNumber}. {song.Title}");
            }
        }

        foreach (var mismatch in allMismatches)
            output.AppendLine($"MISMATCH: {mismatch}");

        return output.ToString();
    }

    public static void Delete(DocumentStore store, string name) => store.Delete(name);

    public static void Rename(DocumentStore store, string name, string newName) => store.Rename(name, newName);

    /// <summary>
    /// Builds a document's leaflet: loads it (resolving its folder via the index), groups its
    /// songs by chapter, calls <see cref="LeafletBuilder.Build"/>, writes each non-empty
    /// chapter's `chapter.tex` under its own `Chapters/&lt;name&gt;/` folder, and writes the main
    /// document (which `\input`s each chapter, per `sb/specification.md` §2.5.1/§4) to
    /// `&lt;document's indexed path&gt;/&lt;document's --name&gt;.tex` (contracts/cli-commands.md's
    /// `documents build`). Returns the main document's written path.
    /// </summary>
    public static string Build(
        DocumentStore documents,
        ChapterStore chapters,
        SongStore songs,
        LocationMismatchDetector detector,
        string documentName,
        OrderingMode ordering = OrderingMode.AsListed,
        int maxPermutations = 100,
        LeafletFormat format = LeafletFormat.SingleColumn)
    {
        var document = documents.Find(documentName);

        var mismatches = detector.DetectAll()
            .Where(m => m.ExpectedPath.StartsWith(document.Path, StringComparison.Ordinal))
            .ToList();
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot build '{documentName}': the index and filesystem disagree:\n" +
                string.Join("\n", mismatches));
        }

        var leafletChapters = chapters.List(documentName)
            .Select(chapter => new LeafletChapter(chapter.Name, chapter.Title, songs.List(documentName, chapter.Name)))
            .ToList();

        // LeafletRequest's constructor rejects a document with zero songs across all its
        // chapters (FR-012) and a non-positive maxPermutations with descriptive errors — no need
        // to duplicate those checks here. --format is supplied per `build` invocation (spec.md's
        // 2026-09-26 Clarification), not stored on the Document.
        var request = new LeafletRequest(document.Title, leafletChapters, format, ordering, maxPermutations, document.Subtitle);
        var leaflet = LeafletBuilder.Build(request);

        foreach (var chapterFile in leaflet.ChapterFiles)
        {
            var chapterPath = chapters.ResolveChapterPath(document.Path, chapterFile.ChapterName);
            File.WriteAllText(Path.Combine(chapterPath, "chapter.tex"), chapterFile.Content, TextEncoding.Utf8NoBom);
        }

        var outputPath = Path.Combine(document.Path, $"{document.Name}.tex");
        File.WriteAllText(outputPath, leaflet.Document, TextEncoding.Utf8NoBom);
        return outputPath;
    }
}
