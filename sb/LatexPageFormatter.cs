using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace sb;

internal static class LatexPageFormatter
{

    //\section defaults : {3.5ex plus 1ex minus .2ex}{2.3ex plus .2ex}
    
    //These should not be constant, they should be specified by user or default values
    #region page values

    private const double VSPACE_PREF = 8.5;
    public const double MINIPAGE_WIDTH_PERCENTAGE = 0.475;

    private const double LONG = 252;
    public const double SHORT = 195;

    public const double SIDE_MARGINS = 10.2;
    private const double TOP_MARGIN = 10.0;
    private const double BOTTOM_MARGIN = 16.5;

    public const double TEXT_WIDTH = 556;
    public const double TEXT_HEIGHT = 726;
    #endregion
    

    private const double EX = 1.51323;
    private const double PT = 0.3515;
    private const double FONT_SIZE = 12 * PT;
    private const double BASE_LINE_SKIP = 1.2 * FONT_SIZE;
    private const double SECTION_SPACE = EX * (3.5 + 0.2);

    public static (string, string, double) CalculateOverflowRemedy(Song song) {
        const double total_margin_mm = TOP_MARGIN + BOTTOM_MARGIN;
        const double total_header_mm = BASE_LINE_SKIP * 3 + VSPACE_PREF;
        const int first_column_max_lines = (int)((LONG - total_header_mm - total_margin_mm) / BASE_LINE_SKIP);
        Console.WriteLine($"Total margin mm: {total_margin_mm}, total header mm: {total_header_mm}, max lyric lines in first column: {first_column_max_lines}");

        int lastIndex = 0;
        int linesThatfit = 0;
        foreach (LatexParagraph t in song.paragraphs) {
            if (linesThatfit + t.LineSpan > first_column_max_lines) break;
            linesThatfit += t.LineSpan;
            lastIndex++;
        }

        StringBuilder sb = new();
        for (int i = 0; i < lastIndex; i++) {
            foreach (string line in song.paragraphs[i].Lines) {
                if (line.Length == 0 && i != lastIndex - 1)  {
                    sb.AppendLine().AppendLine(@"\noindent");
                }
                else sb.AppendLine(line + @"\\");
            }
        }
        string ret1 = sb.ToString();
        sb.Clear();
        int linesInSecondColumn = 0;
        for (int i = lastIndex; i < song.paragraphs.Count; i++) {
            foreach (string line in song.paragraphs[i].Lines) {
                if (line.Length == 0 && i != song.paragraphs.Count - 1)  {
                    sb.AppendLine().AppendLine(@"\noindent");
                }
                else sb.AppendLine(line + @"\\");

                linesInSecondColumn++;
            }
        }

        string ret2 = sb.ToString();
        double underflow = BASE_LINE_SKIP * (linesThatfit - linesInSecondColumn);
        
        Console.WriteLine("Total lines in song: " + song.lineCount);
        Console.WriteLine("Lines to move to second column: " + linesInSecondColumn);
        Console.WriteLine("Second column underflow: " + underflow);

        return (ret1, ret2, underflow);
    }

    public static List<LatexPage> FindOptimalFitForMinipages(params Song[] songs) {
        List<Song[]> combinations = songs.CreateRelevantCombinationsList().ToList();
        List<Song> remaining = new(songs);
        List<LatexPage> pages = new();
        
        while (remaining.Count > 0) {
            int combinationIndex = 0;
            double underflow = int.MaxValue;
            for (int i = 0; i < combinations.Count; i++) {
                double fit = CalculateTwoColumnOverflow(combinations[i]);
                if (fit < 0) continue;
                if (!(underflow > fit) || !(fit > 0)) continue;
                underflow = fit;
                combinationIndex = i;
            }
            Console.WriteLine($"Best fit: {underflow}, Index of Best fit: {combinationIndex}");
            Console.WriteLine("Songs in combination:");

            for (int i1 = 0; i1 < combinations[combinationIndex].Length; i1++) {
                Console.WriteLine($"  {combinations[combinationIndex][i1].title}");
            }

            LatexPage page = new(combinations[combinationIndex], underflow, LatexFormattingOption.Leaflet);
            for (int i = 0; i < combinations[combinationIndex].Length; i++)
                remaining.Remove(combinations[combinationIndex][i]);
            combinations = combinations.Where(x => !x.Intersect(combinations[combinationIndex]).Any()).ToList();
            pages.Add(page);
        }
        //Calculate actual LaTeX code somewhere else

        return pages;
    }
    
    public static List<LatexPage> FindOptimalFitForStandalonePages(params Song[] songs) {

        List<Song[]> combinations = songs.CreateRelevantCombinationsList().ToList();
        List<Song> remaining = new(songs);
        List<LatexPage> pages = new();
        
        while(remaining.Count > 0) {
            int combinationIndex = 0;
            double underflow = int.MaxValue;
            for (int i = 0; i < combinations.Count; i++) {
                if (!IsOnePage(combinations[i])) continue;
                double fit = CalculateStandaloneOverflow(combinations[i]);
                if (underflow > fit && fit > 0) {
                    underflow = fit;
                    combinationIndex = i;
                }
            }
            Trace.WriteLineIf(underflow > 1000, $"Page span for: {combinations[combinationIndex][0].title} {CalculatePageSpanStandalone(combinations[combinationIndex][0])}" );
            //Idk what to do with songs that span two pages or more
            //Need to represent it somehow in Song.cs
            //Could use underflow as pageSpan?

            Trace.WriteLine($"Best fit: {underflow}, Index of Best fit: {combinationIndex}");
            Console.WriteLine("Songs in combination:");
            for (int i1 = 0; i1 < combinations[combinationIndex].Length; i1++) {
                Trace.WriteLine($"  {combinations[combinationIndex][i1].title}");
            }
            
            LatexPage page = new(combinations[combinationIndex]);
            for (int i = 0; i < combinations[combinationIndex].Length; i++) 
                remaining.Remove(combinations[combinationIndex][i]);
            combinations = combinations.Where(x => !x.Intersect(combinations[combinationIndex]).Any()).ToList();
            
            pages.Add(page);
        }
        return pages;
    }

    private static bool IsOnePage(params Song[] songs) {
        return CalculateSingleColumnOverFlow(songs) >= 0;
    }

    private static double CalculateSingleColumnOverFlow(params Song[] songs) {
        double total_space_used_by_songs = (
            from song in songs 
            select BASE_LINE_SKIP * song.lineCount 
            into total_lyric_mm 
            let total_header_mm = BASE_LINE_SKIP * 3 + VSPACE_PREF 
            select total_lyric_mm + total_header_mm).Sum();

        double remaining = LONG - (TOP_MARGIN + BOTTOM_MARGIN + total_space_used_by_songs);

        return remaining;
    }

    private static double CalculateTwoColumnOverflow(params Song[] songs) {
        double total_space_used_by_songs = (
            from song in songs 
            select BASE_LINE_SKIP * song.lineCount 
            into total_lyric_mm 
            let total_header_mm = BASE_LINE_SKIP * 3 + VSPACE_PREF 
            select total_lyric_mm + total_header_mm).Sum();

        double remaining = 2*LONG - (2*(TOP_MARGIN + BOTTOM_MARGIN) + total_space_used_by_songs);

        return remaining;
    }

    public static double CalculateTotalSpace(this Song song, LatexFormattingOption format) {
        double total_lyric_mm = BASE_LINE_SKIP * song.lineCount;
        const double total_header_mm = BASE_LINE_SKIP * 3 + VSPACE_PREF;
        double total_space_used_by_song = format == LatexFormattingOption.Book
            ? total_lyric_mm + total_header_mm + SECTION_SPACE
            : total_lyric_mm + total_header_mm;
        return total_space_used_by_song;
    }
    
    private static double CalculateStandaloneOverflow(params Song[] songs) {
        double total_space_used_by_songs = (
             from song in songs 
             select BASE_LINE_SKIP * song.lineCount 
             into total_lyric_mm 
             let total_header_mm = BASE_LINE_SKIP * 2 + VSPACE_PREF + SECTION_SPACE
             select total_lyric_mm + total_header_mm).Sum();
         

        double remaining = LONG - (TOP_MARGIN + BOTTOM_MARGIN + total_space_used_by_songs);

        return remaining;
    }


    private static double CalculatePageSpanStandalone(Song song) {

        double songSpace = TOP_MARGIN + BOTTOM_MARGIN + SECTION_SPACE + song.lineCount * BASE_LINE_SKIP + BASE_LINE_SKIP;
        
        return songSpace / LONG;
    }
    
    private static double CalculatePageSpanMinipage(Song song) {
        const double total_margin_mm = TOP_MARGIN + BOTTOM_MARGIN;
        const double total_header_mm = BASE_LINE_SKIP * 3 + VSPACE_PREF;
        double songSpace = total_header_mm + total_margin_mm + song.lineCount * BASE_LINE_SKIP + BASE_LINE_SKIP;
        
        return songSpace / (LONG * 2);
    }

    public static string CreateOutputFromInput(LatexFormattingOption format, params Song[] songs) {
        return format switch {
            LatexFormattingOption.Book => CreateStandalones(songs),
            LatexFormattingOption.Leaflet => CreateMinipages(songs),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }


    public static string CreateMinipages(params Song[] songs) {
        //Hit kommer sånger
        
        List<LatexPage> pages = FindOptimalFitForMinipages(songs);
        StringBuilder sb = new();

        Dictionary<int, string> dict = new();
        for (int index = 0; index < pages.Count; index++) {
            LatexPage? page = pages[index];
            sb.AppendLine(@"\newpage");
            for (int i = 0; i < page.Songs.Length; i++) {
                Song? song = page.Songs[i];
                sb.Append(helper2(index));
                sb.Append(
                    $$"""
                    \begin{minipage}{{{MINIPAGE_WIDTH_PERCENTAGE.ToString(CultureInfo.InvariantCulture)}}\textwidth}
                        \paragraph*{{{song.title}}\\}
                        \vspace{{{VSPACE_PREF.ToString(CultureInfo.InvariantCulture)}}mm}
                        \textit{Mel: {{song.melody}}}\\
                    \end{minipage}{{helper(i)}}
                    """);
            }
        }

        return sb.ToString();
    }

    private static string helper2(int index) {
        return index % 2 == 0 ? "\\noindent" : "";
    }

    private static string helper(int index) {
        return index % 2 == 0 ? "%\n\\hspace{0.05\\textwidth}\n" : "\n\n";
    }

    private static string CreateStandalones(params Song[] songs) {
        //Hit kommer oxå sånger (min hjärna fungerar)

        List<LatexPage> pages = FindOptimalFitForStandalonePages(songs);

        StringBuilder sb = new();
        
        foreach (LatexPage page in pages) {
            sb.AppendLine(@"\newpage");
            foreach (Song song in page.Songs) {
                string h = $$"""
                        \section*{{{song.title}}}
                        \textit{Mel: {{song.melody}}}\\

                        \noindent
                        {{song.lyrics}}
                        """;
                sb.AppendLine(h);
            }
        }

        return sb.ToString();
    }

    private static string CreateStandalonesWithScore(params Song[] songs) {
        StringBuilder sb = new();


        return sb.ToString();
    }
}