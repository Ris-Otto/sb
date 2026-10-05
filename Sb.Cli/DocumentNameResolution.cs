using Sb.Core;
using Sb.Core.Documents;
using Sb.Core.Storage;

namespace Sb.Cli;

/// <summary>
/// Resolves a command's document-name from the value given on the command line, falling back to
/// working-directory inference (FR-1.8.4) when it's omitted. Only usable for commands where
/// document-name is the sole positional argument — see <see cref="DocumentsCommands"/>,
/// <see cref="ChaptersCommands"/>, and <see cref="SongsCommands"/> for which commands that is and why:
/// System.CommandLine has no way to leave a *leading* positional argument unfilled when other required
/// positionals follow it (confirmed empirically — with fewer tokens than declared positions, it
/// reports a missing argument rather than skipping the optional one), so commands with more than one
/// positional keep document-name required and don't support inference.
/// </summary>
public static class DocumentNameResolution
{
    public static Result<string> Resolve(string? given, Database database)
    {
        if (given is not null)
            return Result<string>.Success(given);

        string? inferred = WorkingDirectoryInference.Infer(database, Directory.GetCurrentDirectory()).DocumentName;
        return inferred is not null
            ? Result<string>.Success(inferred)
            : Result<string>.Failure(new Error(
                ErrorKind.Validation,
                "No document name given, and the current directory isn't inside a document. Pass a document " +
                "name, or run this command from inside a document's folder."));
    }
}
