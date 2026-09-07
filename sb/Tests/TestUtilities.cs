using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace sb.Tests;

public class TestUtilities
{
    
    private const string path = @"C:/Users/Ris-Otto/RiderProjects/sb/sb/Tests/TestFiles";

    
    public static List<Song> PopulateAsSongs() {
        DirectoryInfo testDir = new(path);
        List<Song> songs = new ();
        
        List<FileInfo> files = testDir.EnumerateFiles().ToList();
        foreach (FileInfo fileInfo in files) {
            string songName = fileInfo.Name[..fileInfo.Name.IndexOf('.')];
            Song song = new(songName, "", fileInfo.FullName);
            songs.Add(song);
        }

        return songs;
    }

    public static string ParseLatexFile() {
        if (!File.Exists(path + "/TeX/test.tex")) throw new ArgumentException();
        Trace.WriteLine(path + "/TeX/test.tex");
        using StreamReader sr = new(File.OpenRead(path + "/TeX/test.tex"));
        string ret = sr.ReadToEnd();

        return ret;
    }
}