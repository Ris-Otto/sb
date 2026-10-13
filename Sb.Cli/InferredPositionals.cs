using System.CommandLine;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

/// <summary>
/// Positional arguments that start with an optional document-name (and, for `songs add`, an optional
/// chapter-name) which are inferred from the working directory when left out (FR-1.8.4, NFR-2.3.2).
///
/// System.CommandLine fills positional arguments strictly left to right and can't leave a *leading*
/// optional one unfilled while later required ones are given, so these commands declare a single
/// argument holding all of their positionals, with an arity that allows the leading ones to be missing,
/// and <see cref="Resolve"/> assigns them: the last <c>required</c> values are the command's own
/// arguments, and whatever precedes them is the document-name and/or chapter-name, in that order — a
/// lone leading value is the chapter-name for commands that take one, since the document can then be
/// inferred from the same directory.
/// </summary>
internal static class InferredPositionals
{
    public sealed record Resolved(string DocumentName, string? ChapterName, string[] Rest);

    /// <param name="usage">Shown in help, e.g. <c>[document-name] song-title</c>.</param>
    /// <param name="required">How many positionals the command itself takes, after the inferable ones.</param>
    /// <param name="inferableLeading">1 for just document-name, 2 for document-name and chapter-name.</param>
    public static Argument<string[]> Declare(string usage, int required, int inferableLeading) =>
        new("arguments")
        {
            Description = usage + " — document-name" + (inferableLeading > 1 ? " and chapter-name are" : " is") +
                          " inferred from the working directory when omitted.",
            Arity = new ArgumentArity(required, required + inferableLeading)
        };

    public static Result<Resolved> Resolve(string[] values, int required, int inferableLeading, Database database)
    {
        int leading = values.Length - required;
        string[] rest = values[leading..];

        (string? inferredDocument, string? inferredChapter) =
            WorkingDirectoryInference.Infer(database, Directory.GetCurrentDirectory());

        string? document;
        string? chapter = null;
        if (inferableLeading == 1)
        {
            document = leading == 1 ? values[0] : inferredDocument;
        }
        else
        {
            switch (leading)
            {
                case 2:
                    document = values[0];
                    chapter = values[1];
                    break;
                case 1:
                    document = inferredDocument;
                    chapter = values[0];
                    break;
                default:
                    document = inferredDocument;
                    chapter = inferredChapter;
                    break;
            }
        }

        if (document is null)
            return Result<Resolved>.Failure(new Error(
                ErrorKind.Validation,
                "No document name given, and the current directory isn't inside a document. Pass a document " +
                "name, or run this command from inside a document's folder."));

        if (inferableLeading > 1 && chapter is null)
            return Result<Resolved>.Failure(new Error(
                ErrorKind.Validation,
                "No chapter name given, and the current directory isn't inside a chapter. Pass a chapter " +
                "name, or run this command from inside a chapter's folder."));

        return Result<Resolved>.Success(new Resolved(document, chapter, rest));
    }
}
