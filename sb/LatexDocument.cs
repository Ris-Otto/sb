using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace sb;

internal static class Document
{

    private static bool ReadUntil(string match, StreamReader sr, [NotNullWhen(true)] out string? matchedLine) {
        matchedLine = default;
        while (!sr.EndOfStream) {
            string? currLine = sr.ReadLine();
            if(currLine == null || !currLine.Contains(match)) continue;
            matchedLine = currLine;
            return true;
        }
        return false;
    }

    public static async Task CreateNewDefaultDocument(string documentTitle) {
        DirectoryInfo newDir = Directory.CreateDirectory(documentTitle);
        newDir.CreateSubdirectory("Chapters");
        newDir.CreateSubdirectory("Scores");
        newDir.CreateSubdirectory("Images");

        const string thing = """
                \documentclass[oneside, 12pt]{book} %default font size, readable
                \usepackage{lexend}
                \usepackage{etoolbox}
                \usepackage{titlesec}
                \usepackage[margin=0.4in, bmargin=0.65in, tmargin=0.3in]{geometry} %default margins
                \usepackage{fancyhdr}
                \usepackage{eso-pic,graphicx}
                \graphicspath{ {./Images/} }
                \usepackage{background} 
                \titlespacing*{\section} {0pt}{3.5ex plus 1ex minus .2ex}{0ex plus .2ex}
                \begin{document}
                
                    \fancypagestyle{plain}{% Change plain page style
                    \fancyhf{} % clear all header and footer fields 
                    \fancyfoot[C]{\huge\thepage}}
                    \pagestyle{plain}% Apply plain page style

                    %Chapters come here
                    %\input{*relative_path_to_chapter*}
                    %Default path to chapters is Chapters/*chapter_name*

                \end{document}
                """;


        await using FileStream fs = File.Create(newDir.Name + "/" + documentTitle + "-main.tex");
        byte[] output = new UTF8Encoding(true).GetBytes(thing);
        await fs.WriteAsync(output.AsMemory(0, thing.Length));
    }

    public static async Task CreateNewChapter(string documentTitle, string chapterTitle) {
        string path = documentTitle + "/Chapters/" + chapterTitle + ".tex";
        
        if(!File.Exists(path))
            File.Create(path);

        StringBuilder sb = new();
        await using (FileStream fs = File.Open(documentTitle + "/" + documentTitle + "-main.tex", FileMode.Open, FileAccess.ReadWrite)) {
            StreamReader sr = new(fs);
            string chapterInput = $$"""    \input{Chapters/{{chapterTitle}}}""";
            while (!sr.EndOfStream) {
                string? currLine = await sr.ReadLineAsync();
                if (currLine == null) continue;
                if(!currLine.Contains(@"\end{document}")) sb.AppendLine(currLine);
                sb.AppendLine(chapterInput).AppendLine("\n").AppendLine(@"\end{document}");

            }
        }

        await using (FileStream fs = File.Open(documentTitle + "/" + documentTitle + "-main.tex", FileMode.Open, FileAccess.ReadWrite)) {
            byte[] output = new UTF8Encoding(true).GetBytes(sb.ToString());
            await fs.WriteAsync(output.AsMemory(0, sb.ToString().Length));
        }
    }

    public static void AppendToChapter(string documentName, string chapterName, LatexPage page) {
        string path = documentName + "/Chapters/" + chapterName + ".tex";
        if(!File.Exists(path))
            File.Create(path);
        
        string latex = page.RebuildLatex();
        using FileStream fs = File.Open(documentName + "/Chapters/" + chapterName + ".tex", FileMode.Open, FileAccess.ReadWrite);
        StreamReader sr = new(fs);
        sr.ReadToEnd();
            
        byte[] output = new UTF8Encoding(true).GetBytes(latex);
        fs.Write(output, 0, output.Length);
    }
}