using System.CommandLine;
using System.Text;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        // Ensures printed Swedish characters (å/ä/ö) render correctly regardless of the terminal's
        // default code page (FR-1.1.2, NFR-2.4.2) — can throw if there's no attached console (e.g.
        // certain hosting/redirection scenarios), which is harmless to ignore here.
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
        }

        // SB_DATA_DIR overrides where the database lives — lets a user keep more than one separate sb
        // database, and lets tests exercise the real CLI executable without touching the real one.
        string appDataFolder = Environment.GetEnvironmentVariable("SB_DATA_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sb");
        Directory.CreateDirectory(appDataFolder);
        string databasePath = Path.Combine(appDataFolder, "sb.db");

        // Pooling=False: each `sb` invocation is its own short-lived process that exits right after
        // one command, so connection pooling buys nothing — it would only risk leaving the database
        // file locked by a pooled native handle after the process has otherwise finished (see
        // DatabaseTests' own note on this same Microsoft.Data.Sqlite behavior).
        using Database database = Database.Open($"Data Source={databasePath};Pooling=False");
        var documentOperations = new DocumentOperations(database);
        var chapterOperations = new ChapterOperations(database);
        var songOperations = new SongOperations(database);

        var root = new RootCommand("sb - an interface between the user and LaTeX for building song leaflets (sångblad).");
        root.Subcommands.Add(DocumentsCommands.Build(documentOperations, database, ConfirmFromConsole));
        root.Subcommands.Add(ChaptersCommands.Build(chapterOperations, database, ConfirmFromConsole));
        root.Subcommands.Add(SongsCommands.Build(songOperations, database));

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
