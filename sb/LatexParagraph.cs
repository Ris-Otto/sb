using System.Collections.Generic;

namespace sb;

public class LatexParagraph
{
    
    public int LineSpan { get; set; }
    public int TrimmedLineSpan { get; }
    public List<string> Lines { get; } = new();


    public LatexParagraph(int lineSpan, int trimmedLineSpan, List<string> lines) {
        LineSpan = lineSpan;
        TrimmedLineSpan = trimmedLineSpan;
        Lines = lines;
    }

    public LatexParagraph() {
        
    }
}