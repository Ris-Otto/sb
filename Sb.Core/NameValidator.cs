namespace Sb.Core;

/// <summary>
/// Validates a document/chapter/song name before it's used as a folder/file name on disk
/// (FR-1.8.8). The banned set is a platform-independent superset of what Windows rejects, so a
/// name accepted here is safe on every platform SB runs on (NFR-2.4.1), not just the one it
/// happened to be validated on: path separators and other reserved characters, <c>.</c> and
/// <c>..</c>, leading/trailing whitespace, a trailing dot (Windows silently strips it from a folder
/// name, so the folder would not have the name SB recorded), and Windows' reserved device names
/// (CON, NUL, COM1, ...), which cannot be created as a file or folder even with an extension.
/// </summary>
public static class NameValidator
{
    private static readonly char[] IllegalCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <param name="allowTrailingDot">Song titles become the stem of a file name (<c>Title.yml</c>), so a
    /// trailing dot is harmless for them; document and chapter names are folder names, where it is not.</param>
    public static Result Validate(string name, bool allowTrailingDot = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new Error(ErrorKind.Validation, "A name cannot be empty."));

        if (name != name.Trim())
            return Result.Failure(new Error(
                ErrorKind.Validation, "A name cannot start or end with whitespace.", name));

        if (name == ".")
            return Result.Failure(new Error(ErrorKind.Validation, "A name cannot be '.'.", name));

        if (name.Contains(".."))
            return Result.Failure(new Error(ErrorKind.Validation, "A name cannot contain '..'.", name));

        foreach (char c in name)
        {
            if (c < 32 || IllegalCharacters.Contains(c))
                return Result.Failure(
                    new Error(ErrorKind.Validation, $"A name cannot contain the character '{c}'.", name));
        }

        if (!allowTrailingDot && name.EndsWith('.'))
            return Result.Failure(new Error(ErrorKind.Validation, "A name cannot end with '.'.", name));

        // Windows treats "CON", "CON.yml" and "con.anything" alike: only the part before the first dot counts.
        string stem = name.Split('.')[0].TrimEnd();
        if (ReservedDeviceNames.Contains(stem))
            return Result.Failure(new Error(
                ErrorKind.Validation, $"'{stem}' is a reserved name on Windows and cannot be used.", name));

        return Result.Success();
    }
}
