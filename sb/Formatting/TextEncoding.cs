using System.Text;

namespace sb.Formatting;

/// <summary>
/// Shared UTF-8-without-byte-order-mark encoding for every file SB writes (003-utf8-encoding,
/// research.md §3). <see cref="Encoding.UTF8"/>'s own preamble is a 3-byte BOM — passing it
/// explicitly to <see cref="File.WriteAllText(string, string?, Encoding)"/> would add one where
/// the parameterless overload's implicit default never did, and a leading BOM is a known source
/// of LaTeX compilation quirks in generated `.tex` output.
/// </summary>
public static class TextEncoding
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
