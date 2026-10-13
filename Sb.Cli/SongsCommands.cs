using System.CommandLine;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

/// <summary>Builds the `songs` command group (FR-1.11): thin argument parsing and result-to-exit-
/// code/console translation only — all behavior lives in <see cref="SongOperations"/>
/// (NFR-2.2.1/2.2.2). Every subcommand infers document-name (and `add` also chapter-name) from the working
/// directory when it's omitted (FR-1.8.4) — via <see cref="DocumentNameResolution"/> for `list`, whose only
/// positional it is, and via <see cref="InferredPositionals"/> for the commands with further positionals.</summary>
public static class SongsCommands
{
    public static Command Build(SongOperations operations, Database database)
    {
        var songs = new Command("songs", "Manage songs within a document");
        songs.Subcommands.Add(BuildAdd(operations, database));
        songs.Subcommands.Add(BuildDelete(operations, database));
        songs.Subcommands.Add(BuildList(operations, database));
        songs.Subcommands.Add(BuildModify(operations, database));
        return songs;
    }

    private static Command BuildAdd(SongOperations operations, Database database)
    {
        var positionals = InferredPositionals.Declare(
            "[document-name] [chapter-name] title text — the title is the song's display title; the text is the song's text, or a path to a file containing it",
            required: 2, inferableLeading: 2);
        var melody = new Option<string?>("--melody", "-m") { Description = "The song's melody." };
        var writer = new Option<string?>("--writer", "-w") { Description = "The song text's writer." };
        var number = new Option<int?>("--number", "-n")
        {
            Description = "The song's number: its position in the whole document. Defaults to the next sequential number."
        };

        var command = new Command("add", "Add a song to a chapter.");
        command.Arguments.Add(positionals);
        command.Options.Add(melody);
        command.Options.Add(writer);
        command.Options.Add(number);
        command.SetAction(parseResult =>
        {
            Result<InferredPositionals.Resolved> resolved =
                InferredPositionals.Resolve(parseResult.GetValue(positionals)!, 2, 2, database);
            if (resolved.IsFailure)
                return CommandReporting.ReportFailure(parseResult, resolved.Error!);
            (string documentName, string? chapterName, string[] rest) = resolved.Value;

            Result<SongRecord> result = operations.Add(
                documentName, chapterName!, rest[0], rest[1], parseResult.GetValue(writer),
                parseResult.GetValue(melody), parseResult.GetValue(number));

            return CommandReporting.Report(
                parseResult, result, record => $"Added song '{record.Title}' (#{record.Number}).");
        });
        return command;
    }

    private static Command BuildDelete(SongOperations operations, Database database)
    {
        var positionals = InferredPositionals.Declare("[document-name] song-title", required: 1, inferableLeading: 1);

        var command = new Command("delete", "Delete a song.");
        command.Arguments.Add(positionals);
        command.SetAction(parseResult =>
        {
            Result<InferredPositionals.Resolved> resolved =
                InferredPositionals.Resolve(parseResult.GetValue(positionals)!, 1, 1, database);
            if (resolved.IsFailure)
                return CommandReporting.ReportFailure(parseResult, resolved.Error!);
            (string documentName, _, string[] rest) = resolved.Value;

            Result result = operations.Delete(documentName, rest[0]);
            return CommandReporting.Report(parseResult, result, () => $"Deleted song '{rest[0]}'.");
        });
        return command;
    }

    private static Command BuildList(SongOperations operations, Database database)
    {
        var documentName = new Argument<string?>("document-name")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "The document to list songs in. Inferred if omitted and run from inside a document's folder."
        };
        var chapterName = new Option<string?>("--chapter-name", "-c") { Description = "Only list songs in this chapter." };

        var command = new Command("list", "List songs in a document.");
        command.Arguments.Add(documentName);
        command.Options.Add(chapterName);
        command.SetAction(parseResult =>
        {
            Result<string> documentNameResult = DocumentNameResolution.Resolve(parseResult.GetValue(documentName), database);
            if (documentNameResult.IsFailure)
                return CommandReporting.ReportFailure(parseResult, documentNameResult.Error!);

            Result<IReadOnlyList<SongEntry>> result =
                operations.List(documentNameResult.Value, parseResult.GetValue(chapterName));

            if (result.IsFailure)
                return CommandReporting.ReportFailure(parseResult, result.Error!);

            PrintListings(parseResult.InvocationConfiguration.Output, result.Value);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command BuildModify(SongOperations operations, Database database)
    {
        var positionals = InferredPositionals.Declare("[document-name] song-title", required: 1, inferableLeading: 1);
        var title = new Option<string?>("--title", "-t") { Description = "The song's new title." };
        var writer = new Option<string?>("--writer", "-w") { Description = "The song text's new writer. An empty value (\"\") removes it." };
        var melody = new Option<string?>("--melody", "-m") { Description = "The song's new melody. An empty value (\"\") removes it." };
        var chapter = new Option<string?>("--chapter", "-c") { Description = "The chapter to move the song to." };
        var number = new Option<int?>("--number", "-n") { Description = "The song's new number (position in the whole document)." };

        var command = new Command("modify", "Modify one or more fields of a song.");
        command.Arguments.Add(positionals);
        command.Options.Add(title);
        command.Options.Add(writer);
        command.Options.Add(melody);
        command.Options.Add(chapter);
        command.Options.Add(number);
        command.SetAction(parseResult =>
        {
            Result<InferredPositionals.Resolved> resolved =
                InferredPositionals.Resolve(parseResult.GetValue(positionals)!, 1, 1, database);
            if (resolved.IsFailure)
                return CommandReporting.ReportFailure(parseResult, resolved.Error!);
            (string documentName, _, string[] rest) = resolved.Value;

            Result result = operations.Modify(
                documentName, rest[0], parseResult.GetValue(title),
                parseResult.GetValue(writer), parseResult.GetValue(melody), parseResult.GetValue(chapter),
                parseResult.GetValue(number));

            return CommandReporting.Report(parseResult, result, () => $"Modified song '{rest[0]}'.");
        });
        return command;
    }

    private static void PrintListings(TextWriter output, IReadOnlyList<SongEntry> entries)
    {
        if (entries.Count == 0)
        {
            output.WriteLine("No songs.");
            return;
        }

        foreach (var group in entries.GroupBy(e => e.ChapterName))
        {
            output.WriteLine($"{group.Key}:");
            foreach (SongEntry entry in group.OrderBy(e => e.Number))
                output.WriteLine($"  {entry.Number}. {entry.Title}{CommandReporting.MissingNote(entry.MissingAt)}");
        }
    }
}
