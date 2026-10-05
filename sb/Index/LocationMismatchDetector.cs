using System.Text;
using sb.Songs;
using YamlDotNet.Serialization;

namespace sb.Index;

/// <summary>
/// Compares every indexed document/chapter/song location against the actual filesystem and
/// reports mismatches (FR-022/FR-023) — it never repairs the index or moves files itself. Also
/// compares each song's file `number` against the index's mirrored singing-order number
/// (research.md §12), applying the same surface-don't-repair rule to that content mismatch.
/// </summary>
public sealed class LocationMismatchDetector
{
    private readonly SbIndexDatabase _index;
    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    public LocationMismatchDetector(SbIndexDatabase index)
    {
        _index = index;
    }

    public IReadOnlyList<MismatchReport> DetectAll()
    {
        var reports = new List<MismatchReport>();
        var documentPaths = new Dictionary<int, string>();

        using (var documentCommand = _index.Connection.CreateCommand())
        {
            documentCommand.CommandText = "SELECT id, path FROM documents;";
            using var reader = documentCommand.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var path = reader.GetString(1);
                documentPaths[id] = path;
                if (!Directory.Exists(path))
                    reports.Add(new MismatchReport(MismatchEntityKind.Document, id, path));
            }
        }

        var chapterPaths = new Dictionary<int, string>();
        using (var chapterCommand = _index.Connection.CreateCommand())
        {
            chapterCommand.CommandText = "SELECT id, name, document_id FROM chapters;";
            using var reader = chapterCommand.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var documentId = reader.GetInt32(2);
                if (!documentPaths.TryGetValue(documentId, out var documentPath))
                    continue; // the parent document's own mismatch was already reported

                var chapterPath = Path.Combine(documentPath, "Chapters", name);
                chapterPaths[id] = chapterPath;
                if (!Directory.Exists(chapterPath))
                    reports.Add(new MismatchReport(MismatchEntityKind.Chapter, id, chapterPath));
            }
        }

        using (var songCommand = _index.Connection.CreateCommand())
        {
            songCommand.CommandText = "SELECT id, name, chapter_id, singing_order_number FROM songs;";
            using var reader = songCommand.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var chapterId = reader.GetInt32(2);
                var indexedNumber = reader.GetInt32(3);
                if (!chapterPaths.TryGetValue(chapterId, out var chapterPath))
                    continue;

                var songFilePath = Path.Combine(chapterPath, $"{name}.yml");
                if (!File.Exists(songFilePath))
                {
                    reports.Add(new MismatchReport(MismatchEntityKind.Song, id, songFilePath));
                    continue;
                }

                var fileNumber = _deserializer.Deserialize<SongFile>(File.ReadAllText(songFilePath, Encoding.UTF8))?.Number;
                if (fileNumber is { } number && number != indexedNumber)
                {
                    reports.Add(new MismatchReport(
                        MismatchEntityKind.SongNumber,
                        id,
                        songFilePath,
                        $"index records singing-order number {indexedNumber}, but '{songFilePath}' has {number}."));
                }
            }
        }

        return reports;
    }
}
