using System.Diagnostics;
using NUnit.Framework;

namespace sb.Tests;

[TestFixture]
public class TestUtilitiesTests
{
    private const string path = @"C:/Users/Ris-Otto/RiderProjects/sb/sb/Tests/TestFiles";
    private readonly DirectoryInfo testDir = new(path);
    
    [Test]
    public void ReadTestFiles() {

        Assert.True(testDir.Exists);
        List<FileInfo> files = testDir.EnumerateFiles().ToList();
        foreach (FileInfo fileInfo in files) {
            Trace.WriteLine(fileInfo.Name);
        }
        Assert.Pass();
    }
    
    [Test]
    public void PopulateAsSongsTest() {

        List<Song> songs = new List<Song>();
        Assert.True(testDir.Exists);
        List<FileInfo> files = testDir.EnumerateFiles().ToList();
        foreach (FileInfo fileInfo in files) {
            string songName = fileInfo.Name[..fileInfo.Name.IndexOf('.')];
            Song song = new(songName, "", fileInfo.FullName);
            songs.Add(song);
            Trace.WriteLine(song);
        }
        Assert.Pass();
    }
}