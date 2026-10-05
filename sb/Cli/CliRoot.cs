using System.CommandLine;
using sb.Chapters;
using sb.Documents;
using sb.Index;
using sb.Leaflets;
using sb.Songs;

namespace sb.Cli;

/// <summary>Assembles the `sb` CLI's full command tree (documents/chapters/songs) and
/// wires each subcommand's flags to the corresponding logic in <c>Cli/*Commands.cs</c>, per
/// contracts/cli-commands.md. (`config` was removed 2026-09-26 — spec.md's 2026-09-26
/// Clarification.) As of 2026-09-26 (a further Clarification), every previously-required
/// `--option` is a positional argument instead, and every remaining (optional) option has a
/// single-letter alias (e.g. `--path`/`-p`).</summary>
public static class CliRoot
{
    public static RootCommand Build(
        DocumentStore documents,
        ChapterStore chapters,
        SongStore songs,
        LocationMismatchDetector detector)
    {
        var root = new RootCommand("sb — turns song text into formatted LaTeX leaflets.");
        root.Subcommands.Add(BuildDocumentsCommand(documents, chapters, songs, detector));
        root.Subcommands.Add(BuildChaptersCommand(chapters, songs));
        root.Subcommands.Add(BuildSongsCommand(songs));
        return root;
    }

    private static Command BuildDocumentsCommand(
        DocumentStore documents,
        ChapterStore chapters,
        SongStore songs,
        LocationMismatchDetector detector)
    {
        var documentsCommand = new Command("documents", "Create, list, and build song leaflets.");

        var nameArgument = new Argument<string>("name") { Description = "Document name." };
        var titleArgument = new Argument<string>("title") { Description = "Display title." };
        var pathOption = new Option<string?>("--path", "-p") { Description = "Folder to create the document at (defaults under the current directory)." };
        var subtitleOption = new Option<string?>("--subtitle", "-s") { Description = "Optional subtitle." };

        var create = new Command("create", "Create a new, empty document.");
        create.Arguments.Add(nameArgument);
        create.Arguments.Add(titleArgument);
        create.Options.Add(pathOption);
        create.Options.Add(subtitleOption);
        create.SetAction(parseResult =>
        {
            var doc = DocumentCommands.Create(
                documents,
                parseResult.GetValue(nameArgument)!,
                parseResult.GetValue(titleArgument)!,
                parseResult.GetValue(pathOption),
                parseResult.GetValue(subtitleOption));
            Console.WriteLine($"Created document '{doc.Name}' at {doc.Path}");
        });

        var chaptersFlag = new Option<bool>("--chapters", "-c") { Description = "Also list each document's chapters." };
        var songsFlag = new Option<bool>("--songs", "-s") { Description = "Also list songs (nested under chapters when --chapters is given)." };

        var list = new Command("list", "List documents created with this tool.");
        list.Options.Add(chaptersFlag);
        list.Options.Add(songsFlag);
        list.SetAction(parseResult =>
        {
            Console.Write(DocumentCommands.List(
                documents,
                detector,
                parseResult.GetValue(chaptersFlag),
                parseResult.GetValue(songsFlag),
                chapters,
                songs));
        });

        var deleteNameArgument = new Argument<string>("name") { Description = "Document to delete." };
        var delete = new Command("delete", "Delete a document and everything under it.");
        delete.Arguments.Add(deleteNameArgument);
        delete.SetAction(parseResult =>
        {
            DocumentCommands.Delete(documents, parseResult.GetValue(deleteNameArgument)!);
            Console.WriteLine("Deleted.");
        });

        var renameNameArgument = new Argument<string>("name") { Description = "Current document name." };
        var renameNewNameArgument = new Argument<string>("new-name") { Description = "New document name." };
        var rename = new Command("rename", "Rename a document.");
        rename.Arguments.Add(renameNameArgument);
        rename.Arguments.Add(renameNewNameArgument);
        rename.SetAction(parseResult =>
        {
            DocumentCommands.Rename(documents, parseResult.GetValue(renameNameArgument)!, parseResult.GetValue(renameNewNameArgument)!);
            Console.WriteLine("Renamed.");
        });

        var buildDocumentNameArgument = new Argument<string>("document-name") { Description = "Document to build." };
        var orderingArgument = new Argument<string>("ordering") { Description = "as-listed or space-efficient.", DefaultValueFactory = _ => "as-listed" };
        var maxPermutationsOption = new Option<int>("--max-permutations", "-m") { Description = "Bound for the space-efficient exact search.", DefaultValueFactory = _ => 100 };
        var buildFormatOption = new Option<string>("--format", "-f") { Description = "single-column or multi-column.", DefaultValueFactory = _ => "single-column" };

        var build = new Command("build", "Build a document's leaflet.");
        build.Arguments.Add(buildDocumentNameArgument);
        build.Arguments.Add(orderingArgument);
        build.Options.Add(maxPermutationsOption);
        build.Options.Add(buildFormatOption);
        build.SetAction(parseResult =>
        {
            var outputPath = DocumentCommands.Build(
                documents,
                chapters,
                songs,
                detector,
                parseResult.GetValue(buildDocumentNameArgument)!,
                ParseOrdering(parseResult.GetValue(orderingArgument)!),
                parseResult.GetValue(maxPermutationsOption),
                ParseFormat(parseResult.GetValue(buildFormatOption)!));
            Console.WriteLine($"Built leaflet at {outputPath}");
        });

        documentsCommand.Subcommands.Add(create);
        documentsCommand.Subcommands.Add(list);
        documentsCommand.Subcommands.Add(delete);
        documentsCommand.Subcommands.Add(rename);
        documentsCommand.Subcommands.Add(build);
        return documentsCommand;
    }

    private static Command BuildChaptersCommand(ChapterStore chapters, SongStore songs)
    {
        var chaptersCommand = new Command("chapters", "Add, remove, and list chapters within a document.");

        var addDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var nameOption = new Option<string?>("--name", "-n") { Description = "Chapter name (defaults to <document>_chp_<n>)." };
        var titleOption = new Option<string?>("--title", "-t") { Description = "Optional display title." };

        var add = new Command("add", "Add a chapter to a document.");
        add.Arguments.Add(addDocumentNameArgument);
        add.Options.Add(nameOption);
        add.Options.Add(titleOption);
        add.SetAction(parseResult =>
        {
            var chapter = ChapterCommands.Add(
                chapters,
                parseResult.GetValue(addDocumentNameArgument)!,
                parseResult.GetValue(nameOption),
                parseResult.GetValue(titleOption));
            Console.WriteLine($"Added chapter '{chapter.Name}'.");
        });

        var removeDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var namesArgument = new Argument<string[]>("names") { Description = "Chapter name(s) to remove." };
        var remove = new Command("remove", "Remove chapter(s) and all songs within them.");
        remove.Arguments.Add(removeDocumentNameArgument);
        remove.Arguments.Add(namesArgument);
        remove.SetAction(parseResult =>
        {
            ChapterCommands.Remove(chapters, parseResult.GetValue(removeDocumentNameArgument)!, parseResult.GetValue(namesArgument)!);
            Console.WriteLine("Removed.");
        });

        var listDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var songsFlag = new Option<bool>("--songs", "-s") { Description = "Also list songs under each chapter." };
        var list = new Command("list", "List a document's chapters.");
        list.Arguments.Add(listDocumentNameArgument);
        list.Options.Add(songsFlag);
        list.SetAction(parseResult =>
        {
            Console.Write(ChapterCommands.List(
                chapters,
                parseResult.GetValue(listDocumentNameArgument)!,
                parseResult.GetValue(songsFlag),
                songs));
        });

        chaptersCommand.Subcommands.Add(add);
        chaptersCommand.Subcommands.Add(remove);
        chaptersCommand.Subcommands.Add(list);
        return chaptersCommand;
    }

    private static Command BuildSongsCommand(SongStore songs)
    {
        var songsCommand = new Command("songs", "Add, remove, and list songs.");

        var addDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var addChapterNameArgument = new Argument<string>("chapter-name") { Description = "Parent chapter." };
        var addTitleArgument = new Argument<string>("title") { Description = "Display title." };
        var addTextArgument = new Argument<string>("text") { Description = "Lyric text, or a path to an existing file containing it." };
        var addSingingNumberArgument = new Argument<int>("singing-number") { Description = "The order this song should be sung in." };
        var writerOption = new Option<string?>("--writer", "-w") { Description = "Optional writer attribution." };
        var melodyOption = new Option<string?>("--melody", "-m") { Description = "Optional melody reference." };

        var add = new Command("add", "Add a song to a chapter.");
        add.Arguments.Add(addDocumentNameArgument);
        add.Arguments.Add(addChapterNameArgument);
        add.Arguments.Add(addTitleArgument);
        add.Arguments.Add(addTextArgument);
        add.Arguments.Add(addSingingNumberArgument);
        add.Options.Add(writerOption);
        add.Options.Add(melodyOption);
        add.SetAction(parseResult =>
        {
            var song = SongCommands.Add(
                songs,
                parseResult.GetValue(addDocumentNameArgument)!,
                parseResult.GetValue(addChapterNameArgument)!,
                parseResult.GetValue(addTitleArgument)!,
                parseResult.GetValue(addTextArgument)!,
                parseResult.GetValue(addSingingNumberArgument),
                parseResult.GetValue(writerOption),
                parseResult.GetValue(melodyOption));
            Console.WriteLine($"Added song '{song.Title}'.");
        });

        var removeDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var songNameArgument = new Argument<string>("song-name") { Description = "Song to remove (matched by title)." };
        var remove = new Command("remove", "Remove a song.");
        remove.Arguments.Add(removeDocumentNameArgument);
        remove.Arguments.Add(songNameArgument);
        remove.SetAction(parseResult =>
        {
            SongCommands.Remove(songs, parseResult.GetValue(removeDocumentNameArgument)!, parseResult.GetValue(songNameArgument)!);
            Console.WriteLine("Removed.");
        });

        var listDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var chapterFilterOption = new Option<string?>("--chapter-name", "-c") { Description = "Restrict listing to one chapter." };
        var list = new Command("list", "List songs.");
        list.Arguments.Add(listDocumentNameArgument);
        list.Options.Add(chapterFilterOption);
        list.SetAction(parseResult =>
        {
            Console.Write(SongCommands.List(
                songs,
                parseResult.GetValue(listDocumentNameArgument)!,
                parseResult.GetValue(chapterFilterOption)));
        });

        var modifyDocumentNameArgument = new Argument<string>("document-name") { Description = "Parent document." };
        var modifySongNameArgument = new Argument<string>("song-name") { Description = "Song to modify." };
        var modifyTitleOption = new Option<string?>("--title", "-t") { Description = "New title." };
        var modifyWriterOption = new Option<string?>("--writer", "-w") { Description = "New writer." };
        var modifyMelodyOption = new Option<string?>("--melody", "-m") { Description = "New melody." };
        var modifyChapterOption = new Option<string?>("--chapter", "-c") { Description = "Move the song to this chapter (does not change its singing-order number)." };
        var modifySingingNumberOption = new Option<int?>("--singing-number", "-s") { Description = "New singing-order number." };

        var modify = new Command("modify", "Modify an existing song's title, writer, melody, chapter, or singing-order number.");
        modify.Arguments.Add(modifyDocumentNameArgument);
        modify.Arguments.Add(modifySongNameArgument);
        modify.Options.Add(modifyTitleOption);
        modify.Options.Add(modifyWriterOption);
        modify.Options.Add(modifyMelodyOption);
        modify.Options.Add(modifyChapterOption);
        modify.Options.Add(modifySingingNumberOption);
        modify.SetAction(parseResult =>
        {
            SongCommands.Modify(
                songs,
                parseResult.GetValue(modifyDocumentNameArgument)!,
                parseResult.GetValue(modifySongNameArgument)!,
                parseResult.GetValue(modifyTitleOption),
                parseResult.GetValue(modifyWriterOption),
                parseResult.GetValue(modifyMelodyOption),
                parseResult.GetValue(modifyChapterOption),
                parseResult.GetValue(modifySingingNumberOption));
            Console.WriteLine("Modified.");
        });

        songsCommand.Subcommands.Add(add);
        songsCommand.Subcommands.Add(remove);
        songsCommand.Subcommands.Add(list);
        songsCommand.Subcommands.Add(modify);
        return songsCommand;
    }

    private static LeafletFormat ParseFormat(string text) => text switch
    {
        "single-column" => LeafletFormat.SingleColumn,
        "multi-column" => LeafletFormat.MultiColumn,
        _ => throw new InvalidOperationException($"Unknown --format '{text}'. Expected 'single-column' or 'multi-column'.")
    };

    private static Ordering.OrderingMode ParseOrdering(string text) => text switch
    {
        "as-listed" => Ordering.OrderingMode.AsListed,
        "space-efficient" => Ordering.OrderingMode.SpaceEfficient,
        _ => throw new InvalidOperationException($"Unknown ordering '{text}'. Expected 'as-listed' or 'space-efficient'.")
    };
}
