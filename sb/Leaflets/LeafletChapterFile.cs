namespace sb.Leaflets;

/// <summary>
/// One chapter's rendered `chapter.tex` content (a `\chapter{...}` heading followed by its songs,
/// laid out per <see cref="LeafletRequest.Format"/>) — not written to disk by
/// <see cref="LeafletBuilder"/> itself; the caller resolves <see cref="ChapterName"/> to an
/// actual file path (see `cli-commands.md`'s `documents build`).
/// </summary>
public sealed class LeafletChapterFile
{
    public string ChapterName { get; }
    public string Content { get; }

    public LeafletChapterFile(string chapterName, string content)
    {
        ChapterName = chapterName;
        Content = content;
    }
}
