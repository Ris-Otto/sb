using System.CommandLine;
using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

/// <summary>Builds the `songs` command group (FR-1.11): thin argument parsing and result-to-exit-
/// code/console translation only — all behavior lives in <see cref="SongOperations"/>
/// (NFR-2.2.1/2.2.2).</summary>
public static class SongsCommands
{
    public static Command Build(SongOperations operations)
    {
        var songs = new Command("songs", "Manage songs within a document");
        songs.Subcommands.Add(BuildAdd(operations));
        songs.Subcommands.Add(BuildDelete(operations));
        songs.Subcommands.Add(BuildList(operations));
        songs.Subcommands.Add(BuildModify(operations));
        return songs;
    }

    private static Command BuildAdd(SongOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document to add the song to." };
        var chapterName = new Argument<string>("chapter-name") { Description = "The chapter to add the song to." };
        var title = new Argument<string>("title") { Description = "The song's display title." };
        var text = new Argument<string>("text") { Description = "The song's text, or a path to a file containing it." };
        var melody = new Option<string?>("--melody", "-m") { Description = "The song's melody." };
        var writer = new Option<string?>("--writer", "-w") { Description = "The song text's writer." };
        var number = new Option<int?>("--number", "-n")
        {
            Description = "The song's position in the chapter. Defaults to the next sequential number."
        };

        var command = new Command("add", "Add a song to a chapter.");
        command.Arguments.Add(documentName);
        command.Arguments.Add(chapterName);
        command.Arguments.Add(title);
        command.Arguments.Add(text);
        command.Options.Add(melody);
        command.Options.Add(writer);
        command.Options.Add(number);
        command.SetAction(parseResult =>
        {
            Result<SongRecord> result = operations.Add(
                parseResult.GetValue(documentName)!, parseResult.GetValue(chapterName)!, parseResult.GetValue(title)!,
                parseResult.GetValue(text)!, parseResult.GetValue(writer), parseResult.GetValue(melody),
                parseResult.GetValue(number));

            return CommandReporting.Report(
                parseResult, result, record => $"Added song '{record.Title}' (#{record.Number}).");
        });
        return command;
    }

    private static Command BuildDelete(SongOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document the song belongs to." };
        var songTitle = new Argument<string>("song-title") { Description = "The song to delete." };

        var command = new Command("delete", "Delete a song.");
        command.Arguments.Add(documentName);
        command.Arguments.Add(songTitle);
        command.SetAction(parseResult =>
        {
            Result result = operations.Delete(parseResult.GetValue(documentName)!, parseResult.GetValue(songTitle)!);
            return CommandReporting.Report(parseResult, result, () => $"Deleted song '{parseResult.GetValue(songTitle)}'.");
        });
        return command;
    }

    private static Command BuildList(SongOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document to list songs in." };
        var chapterName = new Option<string?>("--chapter-name", "-c") { Description = "Only list songs in this chapter." };

        var command = new Command("list", "List songs in a document.");
        command.Arguments.Add(documentName);
        command.Options.Add(chapterName);
        command.SetAction(parseResult =>
        {
            Result<IReadOnlyList<SongEntry>> result =
                operations.List(parseResult.GetValue(documentName)!, parseResult.GetValue(chapterName));

            if (result.IsFailure)
                return CommandReporting.ReportFailure(parseResult, result.Error!);

            PrintListings(parseResult.InvocationConfiguration.Output, result.Value);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command BuildModify(SongOperations operations)
    {
        var documentName = new Argument<string>("document-name") { Description = "The document the song belongs to." };
        var songTitle = new Argument<string>("song-title") { Description = "The song to modify." };
        var title = new Option<string?>("--title", "-t") { Description = "The song's new title." };
        var writer = new Option<string?>("--writer", "-w") { Description = "The song text's new writer." };
        var melody = new Option<string?>("--melody", "-m") { Description = "The song's new melody." };
        var chapter = new Option<string?>("--chapter", "-c") { Description = "The chapter to move the song to." };
        var number = new Option<int?>("--number", "-n") { Description = "The song's new position." };

        var command = new Command("modify", "Modify one or more fields of a song.");
        command.Arguments.Add(documentName);
        command.Arguments.Add(songTitle);
        command.Options.Add(title);
        command.Options.Add(writer);
        command.Options.Add(melody);
        command.Options.Add(chapter);
        command.Options.Add(number);
        command.SetAction(parseResult =>
        {
            Result result = operations.Modify(
                parseResult.GetValue(documentName)!, parseResult.GetValue(songTitle)!, parseResult.GetValue(title),
                parseResult.GetValue(writer), parseResult.GetValue(melody), parseResult.GetValue(chapter),
                parseResult.GetValue(number));

            return CommandReporting.Report(parseResult, result, () => $"Modified song '{parseResult.GetValue(songTitle)}'.");
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
                output.WriteLine($"  {entry.Number}. {entry.Title}");
        }
    }
}
