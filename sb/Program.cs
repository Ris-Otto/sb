using System.Text;
using System.Threading.Tasks;
using sb.Chapters;
using sb.Cli;
using sb.Documents;
using sb.Index;
using sb.Songs;

namespace sb;

internal static class Program
{
    /// <summary>
    /// Sets console I/O to UTF-8 (003-utf8-encoding, research.md §1) so non-ASCII output (and,
    /// for interactive input, non-ASCII stdin) is handled correctly during `dotnet run`/`dotnet
    /// test` — the app manifest's `activeCodePage` setting only applies to the published apphost
    /// exe, not this dev-loop path. This is defense-in-depth for display correctness (FR-005); it
    /// does not affect command-line argument decoding, which .NET already handles correctly on
    /// this platform. Swallows any exception, since redirected/piped stdout or stdin (e.g. under a
    /// test runner) can throw when setting these — the CLI must never crash because of it.
    /// </summary>
    internal static void ConfigureUtf8Console()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Redirected/piped stdout or stdin can throw here — non-fatal, safe to ignore.
        }
    }

    public static async Task<int> Main(string[] args)
    {
        ConfigureUtf8Console();

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sbDirectory = Path.Combine(homeDirectory, "sb");
        Directory.CreateDirectory(sbDirectory);

        using var index = new SbIndexDatabase(Path.Combine(sbDirectory, "index.sqlite"));
        var documents = new DocumentStore(index);
        var chapters = new ChapterStore(index, documents);
        var songs = new SongStore(index, chapters, documents);
        var detector = new LocationMismatchDetector(index);

        var rootCommand = CliRoot.Build(documents, chapters, songs, detector);

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
