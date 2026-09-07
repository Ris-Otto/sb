// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.Threading.Tasks;

namespace sb;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        //TODO add image stuff
        //TODO add one by one possibilities
        //SongTextFormatter.ParseFolder(@"C:\Users\Ris-Otto\RiderProjects\sb\sb\Tests\TestFiles");
        //return 0;
        #region Options
        
        Option<string?> songTitleOption = new (
            name: "--title",
            description: "A string containing the name of the song"
        );
        songTitleOption.AddAlias("-t");

        Option<LatexFormattingOption> formatOption = new(name: "--format", description: "book|leaflet");
        formatOption.AddAlias("-f");

        Option<string> documentTitleOption = new(name: "--name", description: "The name of the .tex document/directory. Don't specify file type");
        documentTitleOption.AddAlias("-n");

        Option<string?> songMelodyOption = new (
            name: "--melody",
            description: "A melody description"
        );
        songMelodyOption.AddAlias("-m");

        Option<string?> songTextOption = new(name: "--lyrics",
            description: "Path to a text file or raw text enclosed in citations");

        Option<string?> scoreOption = new(
            name: "--score", 
            description: "Path to score (pdf required). Defaults to ./Scores/score_name if directory not specified");
        scoreOption.AddAlias("-s");
        
        #endregion
        
        #region arguments
        
        Argument<string> chapterAppendArgument = new(name: "chapter",
            description:
            "The chapter to which you would like to append your song. \nIf no chapter is specified the song will be appended to the first chapter");
        
        Argument<string> chapterAddArgument = new(name: "chapter",
            description:
            "The name of the chapter you want to add");
        
        #endregion

        #region Commands
        
        Command documentCommand = new(name: "modify-document", description: "");
        documentCommand.AddAlias("modify");
        
        Command addChapterCommand = new(name: "add-chapter", description: "Creates an empty chapter in ");
        addChapterCommand.AddAlias("add");
        addChapterCommand.AddArgument(chapterAddArgument);
        addChapterCommand.AddOption(documentTitleOption);


        Command appendChapterCommand = new(name: "append-chapter");
        appendChapterCommand.AddAlias("append");
        appendChapterCommand.AddArgument(chapterAppendArgument);
        appendChapterCommand.AddOption(documentTitleOption);
        appendChapterCommand.AddOption(songTitleOption);
        appendChapterCommand.AddOption(songMelodyOption);
        appendChapterCommand.AddOption(songTextOption);
        
        documentCommand.AddCommand(addChapterCommand);
        documentCommand.AddCommand(appendChapterCommand);

        Command createCommand = new(name: "create-document", description: "Create new .tex songbook/-leaflet");
        createCommand.AddAlias("create");
        createCommand.AddOption(documentTitleOption);


        RootCommand rootCommand = new("Tool for creating LaTeX songbooks/-leaflets");
        rootCommand.AddCommand(documentCommand);
        rootCommand.AddCommand(createCommand);
        
        #endregion
        

        #region Handlers
        
        addChapterCommand.SetHandler(async (chapterName, document) => {
            
            await Document.CreateNewChapter(document, chapterName);
            
        }, chapterAddArgument, documentTitleOption);
        
        
        
        appendChapterCommand.SetHandler((chapterName, document, songTitle, songMelody, text) => {
            if (songTitle is null)
            {
                Console.WriteLine("Song title is required");
                return;
            }
            if (File.Exists(text)) {
                LatexPage page = new (songTitle, songMelody, text, null, null);
                Document.AppendToChapter(document, chapterName, page);
                return;
            }

            if (Directory.Exists(text)) {
                LatexPage page = new (SongTextFormatter.ParseFolder(text));
                
                Document.AppendToChapter(document, chapterName, page);
            }

        }, chapterAppendArgument, documentTitleOption, songTitleOption, songMelodyOption, songTextOption);
        
        
        
        createCommand.SetHandler(async (document) => {
            await Document.CreateNewDefaultDocument(document);
        }, documentTitleOption);

        #endregion
        
        
        return await rootCommand.InvokeAsync(args);
    }
}