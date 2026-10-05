using System.CommandLine;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

/// <summary>Builds the `chapters` command group (FR-1.10): thin argument parsing and result-to-
/// exit-code/console translation only — all behavior lives in <see cref="ChapterOperations"/>
/// (NFR-2.2.1/2.2.2).</summary>
public static class ChaptersCommands
{
    public static Command Build(ChapterOperations operations, Func<string, bool> confirm)
    {
        var chapters = new Command("chapters", "Manage chapters within a document");
        chapters.Subcommands.Add(BuildAdd(operations));
        chapters.Subcommands.Add(BuildDelete(operations, confirm));
        chapters.Subcommands.Add(BuildList(operations));
        return chapters;
    }

    private static Command BuildAdd(ChapterOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document to add the chapter to." };
        var name = new Option<string?>("--name", "-n")
        {
            Description = "The chapter's name (used as its folder/file name). Defaults to {document}_chp_{n}."
        };
        var title = new Option<string?>("--title", "-t")
        {
            Description = "The chapter's display title. Defaults to its name."
        };

        var command = new Command("add", "Create an empty chapter within a document.");
        command.Arguments.Add(documentName);
        command.Options.Add(name);
        command.Options.Add(title);
        command.SetAction(parseResult =>
        {
            Result<ChapterRecord> result = operations.Add(
                parseResult.GetValue(documentName)!, parseResult.GetValue(name), parseResult.GetValue(title));

            return CommandReporting.Report(parseResult, result, record => $"Created chapter '{record.Name}'.");
        });
        return command;
    }

    private static Command BuildDelete(ChapterOperations operations, Func<string, bool> confirm)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document the chapters belong to." };
        var chapterNames = new Argument<string>("chapters") { Description = "Comma-separated chapter names to delete." };
        var force = new Option<bool>("--force", "-f") { Description = "Skip the confirmation prompt." };

        var command = new Command("delete", "Delete one or more chapters from a document.");
        command.Arguments.Add(documentName);
        command.Arguments.Add(chapterNames);
        command.Options.Add(force);
        command.SetAction(parseResult =>
        {
            string document = parseResult.GetValue(documentName)!;
            IReadOnlyList<string> names = parseResult.GetValue(chapterNames)!
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            bool forceFlag = parseResult.GetValue(force);

            Result result = operations.Delete(document, names, forceFlag);

            // Delete only ever returns Conflict to mean "this would cascade into non-empty
            // chapter(s); confirm first" — the same unambiguous signal as `documents delete`.
            if (!forceFlag && result.IsFailure && result.Error!.Kind == ErrorKind.Conflict)
            {
                if (!confirm(result.Error!.Message))
                {
                    parseResult.InvocationConfiguration.Output.WriteLine("Aborted; nothing was deleted.");
                    return ExitCodes.Success;
                }

                result = operations.Delete(document, names, force: true);
            }

            return CommandReporting.Report(parseResult, result, () => $"Deleted chapter(s): {string.Join(", ", names)}.");
        });
        return command;
    }

    private static Command BuildList(ChapterOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document to list chapters in." };
        var songs = new Option<bool>("--songs", "-s") { Description = "Include each chapter's songs." };

        var command = new Command("list", "List chapters in a document.");
        command.Arguments.Add(documentName);
        command.Options.Add(songs);
        command.SetAction(parseResult =>
        {
            Result<IReadOnlyList<ChapterListing>> result =
                operations.List(parseResult.GetValue(documentName)!, parseResult.GetValue(songs));

            if (result.IsFailure)
                return CommandReporting.ReportFailure(parseResult, result.Error!);

            PrintListings(parseResult.InvocationConfiguration.Output, result.Value);
            return ExitCodes.Success;
        });
        return command;
    }

    private static void PrintListings(TextWriter output, IReadOnlyList<ChapterListing> listings)
    {
        if (listings.Count == 0)
        {
            output.WriteLine("No chapters.");
            return;
        }

        foreach (ChapterListing chapter in listings)
        {
            output.WriteLine($"{chapter.Name} ({chapter.Title})");
            foreach (SongListing song in chapter.Songs)
                output.WriteLine($"  {song.Number}. {song.Title}");
        }
    }
}
