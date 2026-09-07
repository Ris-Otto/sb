using System.Collections.Generic;
using System.IO;
using System.Text;

namespace sb;

public static class SongTextFormatter
{
    public static List<Song> ParseFolder(string path) {

        if (!Directory.Exists(path)) throw new ArgumentNullException();

        DirectoryInfo info = new (path);
        FileInfo[] fileInfos = info.GetFiles();

        return fileInfos.Select(file => ParseTextFile(file.FullName)).ToList();
    }

    public static Song ParseTextFile(string path) {
        StringBuilder sb = new();
        List <LatexParagraph> paragraphs = new();
        string title = "";
        string melody = "";
        LatexParagraph curr = new();
        int parLineCount = 0;

        FileStream fs = File.OpenRead(path);
        using (StreamReader sr = new(fs)) {
            while (!sr.EndOfStream) {
                string line = sr.ReadLine() ?? string.Empty;
                if (line == string.Empty) {
                    sb.AppendLine().AppendLine(@"\noindent");
                    curr.Lines.Add(line);
                    parLineCount++;
                    curr.LineSpan = parLineCount;
                    paragraphs.Add(curr);
                    curr = new();
                    parLineCount = 0;
                }
                else if (line.StartsWith("t:")) {
                    title = line[2..];
                } else if (line.StartsWith("m:")) {
                    melody = line[2..];
                }
                else {
                    sb.AppendLine(line + @"\\");
                    curr.Lines.Add(line);
                    parLineCount++;
                }
            }

            if (sr.EndOfStream && curr.Lines.Count != 0) {
                curr.LineSpan = parLineCount;
                paragraphs.Add(curr);
            }
        }

        return new(title, melody, sb.ToString(), paragraphs, parLineCount);
    }
}