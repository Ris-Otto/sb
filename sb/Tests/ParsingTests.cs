using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace sb.Tests;


[TestFixture]
public class ParsingTests
{
    private List<Song> _songs; 


    [Test]
    public void ParseSongs() {
        _songs = LatexParser.ParseMiniPageSongs(TestUtilities.ParseLatexFile());

        foreach (Song song in _songs) {
            Trace.Write(song);
        }
    }

    [Test]
    public void ParseFolder() {
        SongTextFormatter.ParseFolder(@"C:\Users\Ris-Otto\RiderProjects\sb\sb\Tests\TestFiles");
    }
}