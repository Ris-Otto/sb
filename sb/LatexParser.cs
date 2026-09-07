using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace sb;

using System.Text.RegularExpressions;

public static class LatexParser
{
    public static List<Song> ParseMiniPageSongs(string latexText)
    {

        const string headerPattern = @"\\begin{minipage}{.*?}\s*\\paragraph\*{(?<title>.*?)\\\\}\s*\\.*\s*\\textit{Mel: (?<melody>.*?)}\\\\";
        const string lyricsPattern = @"\s(?<lyrics>%(.|\s)+?)(?=\\end{minipage})";

        List<Song> songs = new();

        MatchCollection headerMatches = Regex.Matches(latexText, headerPattern);
        MatchCollection lyricsMatches = Regex.Matches(latexText, lyricsPattern);

        int headerIndex = 0;
        int lyricsIndex = 0;

        while (headerIndex < headerMatches.Count)
        {
            Match headerMatch = headerMatches[headerIndex];
            string title = headerMatch.Groups["title"].Value;
            string melody = headerMatch.Groups["melody"].Value;
            string lyrics = "";

            if (lyricsIndex < lyricsMatches.Count)
            {
                Match lyricsMatch = lyricsMatches[lyricsIndex];
                lyrics = lyricsMatch.Groups["lyrics"].Value;
                lyricsIndex++;
            }

            Song song = new(title, melody, lyrics, format: LatexFormattingOption.Leaflet);
            songs.Add(song);

            headerIndex++;
        }

        return songs;
    }
    
    public static List<Song> ParseStandaloneSongs(string latexText)
    {

        const string headerPattern = @"\\section\*{(?<title>.+?)}\n\\textit{Mel: (?<melody>.+?)}";
        const string lyricsPattern = @"\\noindent\s(?<lyrics>(.|\s)*)(?=\\section\*{.*?})";

        List<Song> songs = new();

        MatchCollection headerMatches = Regex.Matches(latexText, headerPattern);
        MatchCollection lyricsMatches = Regex.Matches(latexText, lyricsPattern);

        int headerIndex = 0;
        int lyricsIndex = 0;

        while (headerIndex < headerMatches.Count)
        {
            Match headerMatch = headerMatches[headerIndex];
            string title = headerMatch.Groups["title"].Value;
            string melody = headerMatch.Groups["melody"].Value;
            string lyrics = "";

            if (lyricsIndex < lyricsMatches.Count)
            {
                Match lyricsMatch = lyricsMatches[lyricsIndex];
                lyrics = lyricsMatch.Groups["lyrics"].Value;
                lyricsIndex++;
            }
            
            Song song = new(title, melody, lyrics);
            songs.Add(song);

            headerIndex++;
        }

        return songs;
    }

    private static void RemoveLatexSpecialChars(string latexText) {
        
    }
    
    private static string RemoveTrailingBackslashes(this string content)
    {
        // Split the content into individual lines
        string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string trailing = @"\\";

        // Remove trailing '\\' from each line except for the line break command ('\\\\')
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(trailing)) {
                Trace.WriteLine("??????");
                lines[i] = lines[i][..(lines[i].Length - trailing.Length)];
            }
        }

        // Join the lines back into a single string
        return string.Join(Environment.NewLine, lines);
    }

    private static string? GetScorePath(string section)
    {
        Match match = new Regex("\\\\includepdf\\[.*?\\]{(.*?)}").Match(section);

        if (match is { Success: true, Groups.Count: > 1 })
        {
            return match.Groups[1].Value;
        }

        return null;
    }
}
