using Sb.Core.Storage;

namespace Sb.Core.Documents;

/// <summary>
/// Infers the document (and, if applicable, chapter) a directory belongs to (FR-1.8.4), by checking
/// whether it is or is inside a known document's folder, and — within that — a known chapter's folder.
/// Path comparison is case-insensitive on Windows and case-sensitive elsewhere, matching each
/// platform's own filesystem semantics (NFR-2.4.1).
/// </summary>
public static class WorkingDirectoryInference
{
    public static (string? DocumentName, string? ChapterName) Infer(Database database, string currentDirectory)
    {
        var documents = new DocumentsRepository(database);
        var chapters = new ChaptersRepository(database);

        Result<IReadOnlyList<DocumentRecord>> documentsResult = documents.GetAll();
        if (documentsResult.IsFailure)
            return (null, null);

        string normalizedCwd = Path.GetFullPath(currentDirectory);

        foreach (DocumentRecord document in documentsResult.Value)
        {
            if (!IsSameOrDescendant(normalizedCwd, Path.GetFullPath(document.Path)))
                continue;

            Result<IReadOnlyList<ChapterRecord>> chaptersResult = chapters.GetAllForDocument(document.Id);
            if (chaptersResult.IsSuccess)
            {
                foreach (ChapterRecord chapter in chaptersResult.Value)
                {
                    string chapterPath = Path.GetFullPath(DocumentLayout.ChapterFolder(document.Path, chapter.Name));
                    if (IsSameOrDescendant(normalizedCwd, chapterPath))
                        return (document.Filename, chapter.Name);
                }
            }

            return (document.Filename, null);
        }

        return (null, null);
    }

    private static bool IsSameOrDescendant(string path, string ancestor)
    {
        StringComparison comparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string trimmedAncestor = ancestor.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return trimmedPath.Equals(trimmedAncestor, comparison) ||
               trimmedPath.StartsWith(trimmedAncestor + Path.DirectorySeparatorChar, comparison);
    }
}
