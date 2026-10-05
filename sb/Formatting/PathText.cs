using System.Text;

namespace sb.Formatting;

/// <summary>
/// Guards against Unicode normalization-form mismatches (003-utf8-encoding, research.md §4):
/// the same visible character (e.g. "å") can be represented as either one precomposed code point
/// (NFC) or a base letter plus a combining mark (NFD). If a `--text` value's normalization form
/// differs from however the actual file's name is stored on disk, two visually-identical strings
/// compare unequal and <see cref="File.Exists(string)"/> can return <c>false</c> for a file that
/// is, to a human, obviously right there.
/// </summary>
public static class PathText
{
    public static string NormalizeForFileSystem(string path) => path.Normalize(NormalizationForm.FormC);
}
