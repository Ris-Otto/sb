using System.Reflection;

namespace Sb.Core.LaTeX;

/// <summary>
/// Builds the main `.tex` file for a document from `template-main.tex` (FR-1.12.1), substituting
/// its title/subtitle (FR-1.12.3) and inserting an `\input` line each time a chapter is added
/// (FR-1.12.2). Page metrics (FR-1.12.4) and the absence of an engine-specific package (FR-1.12.9)
/// come from the template itself, preserved verbatim.
/// </summary>
public sealed class DocumentTemplate
{
    private const string ChapterMarker = "%Chapters come here";

    private static readonly string TemplateText = ReadEmbeddedTemplate();

    public string Title { get; }
    public string? Subtitle { get; }
    public IReadOnlyList<string> ChapterPaths { get; }

    private DocumentTemplate(string title, string? subtitle, IReadOnlyList<string> chapterPaths)
    {
        Title = title;
        Subtitle = subtitle;
        ChapterPaths = chapterPaths;
    }

    public static Result<DocumentTemplate> Create(string title, string? subtitle)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<DocumentTemplate>.Failure(new Error(ErrorKind.Validation, "A document must have a title."));

        return Result<DocumentTemplate>.Success(new DocumentTemplate(title, subtitle, []));
    }

    public DocumentTemplate WithChapterAdded(string chapterPath) =>
        new(Title, Subtitle, [.. ChapterPaths, chapterPath]);

    public string Render()
    {
        string chapterInputs = string.Join(
            Environment.NewLine, ChapterPaths.Select(path => $"\\input{{{path}}}"));

        return TemplateText
            .Replace("{title}", LaTeXText.Escape(Title))
            .Replace("{subtitle}", LaTeXText.Escape(Subtitle ?? string.Empty))
            .Replace(ChapterMarker, ChapterPaths.Count == 0 ? ChapterMarker : $"{ChapterMarker}\n{chapterInputs}");
    }

    private static string ReadEmbeddedTemplate()
    {
        Assembly assembly = typeof(DocumentTemplate).Assembly;
        const string resourceName = "Sb.Core.Resources.template-main.tex";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
