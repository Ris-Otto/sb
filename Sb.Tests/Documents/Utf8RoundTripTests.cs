using NUnit.Framework;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Tests.Documents;

/// <summary>Milestone 10.2 (FR-1.1.2, NFR-2.4.2): Swedish characters (å/ä/ö) in names/titles must
/// survive create → list → build unchanged, through the database, the filesystem, and the generated
/// `.tex`.</summary>
[TestFixture]
public class Utf8RoundTripTests
{
    private string _outputFolder = null!;
    private Database _db = null!;

    [SetUp]
    public void SetUp()
    {
        _outputFolder = Path.Combine(Path.GetTempPath(), "sb-utf8-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_outputFolder);
        _db = Database.Open("Data Source=:memory:");
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);
    }

    [Test]
    public void SwedishCharacters_SurviveCreateListBuild_Unchanged()
    {
        var documents = new DocumentOperations(_db);
        var chapters = new ChapterOperations(_db);
        var songs = new SongOperations(_db);

        const string documentName = "sångbok";
        const string documentTitle = "Visor på svenska";
        const string chapterName = "studentsånger";
        const string chapterTitle = "Studentsånger från förr";
        const string songTitle = "Ö i Åmål";
        const string songLine = "Här är en rad, än en rad";

        var createResult = documents.Create(documentName, documentTitle, "Än en bok", _outputFolder);
        Assert.That(createResult.IsSuccess, Is.True);

        var chapterResult = chapters.Add(documentName, chapterName, chapterTitle);
        Assert.That(chapterResult.IsSuccess, Is.True);

        var songResult = songs.Add(documentName, chapterName, songTitle, songLine, writer: "Åke Öberg", melody: null, number: null);
        Assert.That(songResult.IsSuccess, Is.True);

        // Filesystem: folder/file names preserve the characters exactly.
        string documentPath = Path.Combine(_outputFolder, documentName);
        Assert.That(Directory.Exists(documentPath), Is.True);
        string chapterFolder = Path.Combine(documentPath, "Chapters", chapterName);
        Assert.That(Directory.Exists(chapterFolder), Is.True);
        Assert.That(File.Exists(Path.Combine(chapterFolder, $"{songTitle}.yml")), Is.True);

        // Database: list round-trips the Swedish characters in every name/title.
        var listResult = documents.List(includeChapters: true, includeSongs: true);
        Assert.That(listResult.IsSuccess, Is.True);
        var listedDocument = listResult.Value.Single(d => d.Name == documentName);
        Assert.That(listedDocument.Title, Is.EqualTo(documentTitle));
        var listedChapter = listedDocument.Chapters.Single();
        Assert.That(listedChapter.Name, Is.EqualTo(chapterName));
        Assert.That(listedChapter.Title, Is.EqualTo(chapterTitle));
        var listedSong = listedChapter.Songs.Single();
        Assert.That(listedSong.Title, Is.EqualTo(songTitle));

        // Build: the generated .tex contains the characters correctly (å/ä/ö aren't LaTeX-special,
        // so they pass through LaTeXText.Escape unescaped).
        var buildResult = documents.Build(documentName, SongOrdering.AsListed, LeafletFormat.MultiColumn);
        Assert.That(buildResult.IsSuccess, Is.True);

        string mainTex = File.ReadAllText(Path.Combine(documentPath, $"{documentName}.tex"));
        Assert.That(mainTex, Does.Contain(documentTitle));

        string chapterTex = File.ReadAllText(Path.Combine(chapterFolder, $"{chapterName}.tex"));
        Assert.That(chapterTex, Does.Contain(chapterTitle));
        Assert.That(chapterTex, Does.Contain(songTitle));
        Assert.That(chapterTex, Does.Contain(songLine));
    }
}
