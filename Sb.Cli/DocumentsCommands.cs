using System.CommandLine;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

/// <summary>Builds the `documents` command group (FR-1.9): thin argument parsing and result-to-
/// exit-code/console translation only — all behavior lives in <see cref="DocumentOperations"/>
/// (NFR-2.2.1/2.2.2).</summary>
public static class DocumentsCommands
{
    public static Command Build(DocumentOperations operations, Func<string, bool> confirm)
    {
        var documents = new Command("documents", "Manage documents (leaflets)");
        documents.Subcommands.Add(BuildCreate(operations));
        documents.Subcommands.Add(BuildList(operations));
        documents.Subcommands.Add(BuildDelete(operations, confirm));
        documents.Subcommands.Add(BuildRename(operations));
        documents.Subcommands.Add(BuildBuild(operations));
        return documents;
    }

    private static Command BuildCreate(DocumentOperations operations)
    {
        var name = new Argument<string>("name") { Description = "The document's name (used as its folder/file name)." };
        var title = new Argument<string>("title") { Description = "The document's display title." };
        var subtitle = new Option<string?>("--subtitle", "-s") { Description = "The document's display subtitle." };
        var outputFolder = new Option<string>("--output-folder", "-o")
        {
            Description = "The folder the document's own folder is created in.",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };

        var command = new Command("create", "Create an empty document.");
        command.Arguments.Add(name);
        command.Arguments.Add(title);
        command.Options.Add(subtitle);
        command.Options.Add(outputFolder);
        command.SetAction(parseResult =>
        {
            Result<DocumentRecord> result = operations.Create(
                parseResult.GetValue(name)!, parseResult.GetValue(title)!, parseResult.GetValue(subtitle),
                parseResult.GetValue(outputFolder)!);

            return CommandReporting.Report(parseResult, result, record => $"Created document '{record.Filename}' at '{record.Path}'.");
        });
        return command;
    }

    private static Command BuildList(DocumentOperations operations)
    {
        var chapters = new Option<bool>("--chapters", "-c") { Description = "Include each document's chapters." };
        var songs = new Option<bool>("--songs", "-s") { Description = "Include each chapter's songs (requires --chapters)." };

        var command = new Command("list", "List documents.");
        command.Options.Add(chapters);
        command.Options.Add(songs);
        command.SetAction(parseResult =>
        {
            Result<IReadOnlyList<DocumentListing>> result =
                operations.List(parseResult.GetValue(chapters), parseResult.GetValue(songs));

            if (result.IsFailure)
                return CommandReporting.ReportFailure(parseResult, result.Error!);

            PrintListings(parseResult.InvocationConfiguration.Output, result.Value);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command BuildDelete(DocumentOperations operations, Func<string, bool> confirm)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document to delete." };
        var force = new Option<bool>("--force", "-f") { Description = "Skip the confirmation prompt." };

        var command = new Command("delete", "Delete a document and everything inside it.");
        command.Arguments.Add(documentName);
        command.Options.Add(force);
        command.SetAction(parseResult =>
        {
            string name = parseResult.GetValue(documentName)!;
            bool forceFlag = parseResult.GetValue(force);

            Result result = operations.Delete(name, forceFlag);

            // Delete only ever returns Conflict to mean "this would cascade into a non-empty
            // document; confirm first" (it never returns Conflict for a name-uniqueness reason, since
            // nothing here is being named/created) — so it's an unambiguous signal to prompt.
            if (!forceFlag && result.IsFailure && result.Error!.Kind == ErrorKind.Conflict)
            {
                if (!confirm(result.Error!.Message))
                {
                    parseResult.InvocationConfiguration.Output.WriteLine("Aborted; nothing was deleted.");
                    return ExitCodes.Success;
                }

                result = operations.Delete(name, force: true);
            }

            return CommandReporting.Report(parseResult, result, () => $"Deleted document '{name}'.");
        });
        return command;
    }

    private static Command BuildRename(DocumentOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document's current name." };
        var newName = new Argument<string>("new-name") { Description = "The document's new name." };

        var command = new Command("rename", "Rename a document.");
        command.Arguments.Add(documentName);
        command.Arguments.Add(newName);
        command.SetAction(parseResult =>
        {
            Result result = operations.Rename(parseResult.GetValue(documentName)!, parseResult.GetValue(newName)!);
            return CommandReporting.Report(parseResult, result, () => $"Renamed document to '{parseResult.GetValue(newName)}'.");
        });
        return command;
    }

    private static Command BuildBuild(DocumentOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document to build." };
        var ordering = new Option<string>("--ordering", "-o")
        {
            Description = "as-listed | space-efficient",
            DefaultValueFactory = _ => "as-listed"
        };
        ordering.AcceptOnlyFromAmong("as-listed", "space-efficient");
        var format = new Option<string>("--format", "-f")
        {
            Description = "multi-column | single-column",
            DefaultValueFactory = _ => "multi-column"
        };
        format.AcceptOnlyFromAmong("multi-column", "single-column");

        var command = new Command("build", "Build a document's LaTeX source from its chapters and songs.");
        command.Arguments.Add(documentName);
        command.Options.Add(ordering);
        command.Options.Add(format);
        command.SetAction(parseResult =>
        {
            string name = parseResult.GetValue(documentName)!;
            SongOrdering orderingValue = parseResult.GetValue(ordering) switch
            {
                "space-efficient" => SongOrdering.SpaceEfficient,
                _ => SongOrdering.AsListed
            };
            LeafletFormat formatValue = parseResult.GetValue(format) switch
            {
                "single-column" => LeafletFormat.SingleColumn,
                _ => LeafletFormat.MultiColumn
            };

            Result result = operations.Build(name, orderingValue, formatValue);
            return CommandReporting.Report(parseResult, result, () => $"Built document '{name}'.");
        });
        return command;
    }

    private static void PrintListings(TextWriter output, IReadOnlyList<DocumentListing> listings)
    {
        if (listings.Count == 0)
        {
            output.WriteLine("No documents.");
            return;
        }

        foreach (DocumentListing document in listings)
        {
            output.WriteLine($"{document.Name} ({document.Title})");
            foreach (ChapterListing chapter in document.Chapters)
            {
                output.WriteLine($"  {chapter.Name} ({chapter.Title})");
                foreach (SongListing song in chapter.Songs)
                    output.WriteLine($"    {song.Number}. {song.Title}");
            }
        }
    }
}
