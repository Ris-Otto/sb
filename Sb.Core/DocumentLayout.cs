namespace Sb.Core;

/// <summary>
/// Computes the on-disk paths for a document's folder structure (FR-1.5.1–FR-1.5.4): a document is
/// its own folder containing a main `.tex` file and a `Chapters` subfolder, each chapter is its own
/// subfolder there containing a `.tex` file and the chapter's songs as `.yml` files. Centralized here
/// since Milestones 7–9 all need to agree on the same layout.
/// </summary>
public static class DocumentLayout
{
    public static string DocumentFolder(string outputFolder, string documentName) =>
        Path.Combine(outputFolder, documentName);

    public static string MainTexFile(string documentPath, string documentName) =>
        Path.Combine(documentPath, $"{documentName}.tex");

    public static string ChaptersFolder(string documentPath) => Path.Combine(documentPath, "Chapters");

    public static string ChapterFolder(string documentPath, string chapterName) =>
        Path.Combine(ChaptersFolder(documentPath), chapterName);

    public static string ChapterTexFile(string documentPath, string chapterName) =>
        Path.Combine(ChapterFolder(documentPath, chapterName), $"{chapterName}.tex");

    /// <summary>The `\input` path from the main `.tex` file to a chapter's `.tex` file (FR-1.12.2),
    /// relative so the document folder stays portable if moved (NFR-2.5.5).</summary>
    public static string ChapterTexInputPath(string chapterName) => $"Chapters/{chapterName}/{chapterName}.tex";

    public static string SongFile(string documentPath, string chapterName, string songTitle) =>
        Path.Combine(ChapterFolder(documentPath, chapterName), $"{songTitle}.yml");
}
