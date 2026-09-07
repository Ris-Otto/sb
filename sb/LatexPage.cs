using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace sb;

internal sealed class LatexPage
{
	private const double VSPACE_PREF = 8.5;
	private const double MINIPAGE_WIDTH_PERCENTAGE = 0.475;
	private const double HSPACE_PREF = 0.5 - MINIPAGE_WIDTH_PERCENTAGE;

	internal Song[] Songs { get; set; }
	internal double Underflow { get; }
	internal ushort Columns { get; set; }
	internal int ColumnBreakIndex { get; set; }
	public LatexFormattingOption PageFormat { get; set; }

    public LatexPage(
	    string title, 
	    string melody,
	    string path, 
	    string? scorePath,
	    string? composer) {
	    Songs = new Song[1];
	    Songs[0] = new Song(title, melody, path, composer, scorePath);
    }

    public LatexPage(string[] titles,
	    string[] melodies,
	    string[] paths,
	    string[]? scorePaths,
	    string[]? composers) {
	    
	    if(scorePaths != null && composers != null) {
		    Songs = new Song[paths.Length];
		    for (int i = 0; i < paths.Length; i++)
			    Songs[i] = new Song(titles[i], melodies[i], paths[i], composers[i], scorePaths[i]);
		    return;
	    }

	    if (scorePaths != null && composers == null) {
		    Songs = new Song[paths.Length];
		    for (int i = 0; i < paths.Length; i++)
			    Songs[i] = new Song(titles[i], melodies[i], paths[i], score: scorePaths[i]);
		    return;
	    }

	    if (scorePaths == null && composers != null) {
		    Songs = new Song[paths.Length];
		    for (int i = 0; i < paths.Length; i++)
			    Songs[i] = new Song(titles[i], melodies[i], paths[i], composer: composers[i]);
		    return;
	    }

	    throw new ArgumentException("Dogshit values");
    }

    internal LatexPage(IEnumerable<Song> songs, LatexFormattingOption format = LatexFormattingOption.Book) {
	    Songs = songs.ToArray();
	    PageFormat = format;
    }
    
    internal LatexPage(IEnumerable<Song> songs, double underflow,  LatexFormattingOption format) {
	    Songs = songs.ToArray();
	    PageFormat = format;
    }


    private static string CreateMiniPageSong(Song song) {
	    //temp
	    return $$"""
			\newpage
			\noindent
			\begin{minipage}{{{MINIPAGE_WIDTH_PERCENTAGE.ToString(CultureInfo.InvariantCulture)}}\textwidth}
				\paragraph*{{{song.title}}\\}
				\vspace{{{VSPACE_PREF.ToString(CultureInfo.InvariantCulture)}}mm}
				\textit{Mel: {{song.melody}}}\\
			\end{minipage}
				
			\noindent
			\begin{minipage}{{{MINIPAGE_WIDTH_PERCENTAGE.ToString(CultureInfo.InvariantCulture)}}\textwidth}
				{{song.lyrics}}
			\end{minipage}
			""";
    }

    private static StringBuilder CreateStandaloneSong(Song song) {
	    return new StringBuilder($$"""
			\newpage
			\section*{{{song.title}}}
			\vspace{{{VSPACE_PREF}}px}
			\textit{Mel: {{song.melody}}}\\
			\paragraph*{{{song.composer}}}
			$$$$
			{{song.lyrics}}
			""");
    }

    private static string RemedyMiniPageOverFlow(Song song) {
	    var (item1, item2, item3) = LatexPageFormatter.CalculateOverflowRemedy(song);

	    return $$"""
			\newpage
			\noindent
			\begin{minipage}{{{MINIPAGE_WIDTH_PERCENTAGE.ToString(CultureInfo.InvariantCulture)}}\textwidth}
				\paragraph*{{{song.title}}\\}
				\vspace{{{VSPACE_PREF.ToString(CultureInfo.InvariantCulture)}}mm}
				\textit{Mel: {{song.melody}}}\\
			\end{minipage}
				
			\noindent
			\begin{minipage}{{{MINIPAGE_WIDTH_PERCENTAGE.ToString(CultureInfo.InvariantCulture)}}\textwidth}
				{{item1}}
			\end{minipage}%
			\hspace{{{HSPACE_PREF.ToString(CultureInfo.InvariantCulture)}}\textwidth}
			\noindent
			\begin{minipage}{{{MINIPAGE_WIDTH_PERCENTAGE.ToString(CultureInfo.InvariantCulture)}}\textwidth}
				{{item2}}
				\vspace{{{item3.ToString(CultureInfo.InvariantCulture)}}mm}
			\end{minipage}
			""";
    }

    public string RebuildLatex() {
	    return LatexPageFormatter.CreateMinipages(Songs);
    }

    public string ToMetaFileString() {
	    throw new NotImplementedException();
    }
}