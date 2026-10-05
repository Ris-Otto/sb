using System.CommandLine;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sb");
        Directory.CreateDirectory(appDataFolder);
        string databasePath = Path.Combine(appDataFolder, "sb.db");

        using Database database = Database.Open($"Data Source={databasePath}");
        var documentOperations = new DocumentOperations(database);
        var chapterOperations = new ChapterOperations(database);
        var songOperations = new SongOperations(database);

        var root = new RootCommand("sb - an interface between the user and LaTeX for building song leaflets (sångblad).");
        root.Subcommands.Add(DocumentsCommands.Build(documentOperations, ConfirmFromConsole));
        root.Subcommands.Add(ChaptersCommands.Build(chapterOperations, ConfirmFromConsole));
        root.Subcommands.Add(SongsCommands.Build(songOperations));

        return root.Parse(args).Invoke();
    }

    private static bool ConfirmFromConsole(string message)
    {
        Console.Write($"{message} [y/N] ");
        string? response = Console.ReadLine();
        return response is not null &&
               (response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ||
                response.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
