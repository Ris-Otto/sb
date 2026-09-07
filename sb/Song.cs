using System;
using System.Collections.Generic;
using System.Text;

namespace sb;

public sealed class Song : IEquatable<Song>
{
    public readonly string? title;
    public readonly string? melody;
    public readonly string lyrics;
    public readonly List<LatexParagraph> paragraphs;
    public readonly string? composer;
    public readonly int lineCount;
    public readonly string? score;
    public readonly double TotalSpace;

    public const double DEFAULT_VSPACE = 8.5; //mm
    public const double DEFAULT_TEXTWIDTH = 0.475;//\textwidth
    public const double DEFAULT_HSPACE = 0.05;//\textwidth

    public Song(string title, string melody, string path, string? composer = null, string? score = null, LatexFormattingOption format = LatexFormattingOption.Book) {
        Song temp = SongTextFormatter.ParseTextFile(path);
        
        this.title = temp.title??title;
        this.melody = temp.melody??melody;
        
        lyrics = temp.lyrics;
        paragraphs = temp.paragraphs;
        lineCount = temp.lineCount;
        this.composer = composer;
        this.score = score;
        TotalSpace = this.CalculateTotalSpace(format);
    }
    
    public Song(string title, string melody, string lyrics, List<LatexParagraph> pars, int count, string? composer = null, string? score = null, LatexFormattingOption format = LatexFormattingOption.Book) {
        this.title = title;
        this.melody = melody;
        this.lyrics = lyrics;
        paragraphs = pars;
        lineCount = count;
        this.composer = composer;
        this.score = score;
        TotalSpace = this.CalculateTotalSpace(format);
    }
    
    

    public override string ToString() {
        StringBuilder sb = new();
        sb.AppendLine(title);
        sb.AppendLine(melody);
	    
        foreach (LatexParagraph latexParagraph in paragraphs) {
            foreach (string latexParagraphLine in latexParagraph.Lines) {
                sb.AppendLine(latexParagraphLine);
            }
        }
        return sb.ToString();
    }

    public bool Equals(Song? other) {
        if (other is null) return false;
        return title == other.title && melody == other.melody && lyrics == other.lyrics && lineCount == other.lineCount;
    }

    public override bool Equals(object? obj) {
        return obj is Song other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(title, melody, lyrics, lineCount);
    }

    public static bool operator ==(Song? left, Song? right)
    {
        return left != null && left.Equals(right);
    }

    public static bool operator !=(Song? left, Song? right)
    {
        return !(left == right);
    }
}