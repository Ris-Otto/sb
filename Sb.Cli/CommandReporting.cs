using System.CommandLine;
using System.CommandLine.Invocation;
using Sb.Core;

namespace Sb.Cli;

/// <summary>Translates an <see cref="Sb.Core"/> <see cref="Result"/> into console output and an exit
/// code (NFR-2.2.2), shared by every command group so the mapping stays identical across them.</summary>
public static class CommandReporting
{
    public static int Report<T>(ParseResult parseResult, Result<T> result, Func<T, string> successMessage)
    {
        if (result.IsFailure)
            return ReportFailure(parseResult, result.Error!);

        parseResult.InvocationConfiguration.Output.WriteLine(successMessage(result.Value));
        return ExitCodes.Success;
    }

    public static int Report(ParseResult parseResult, Result result, Func<string> successMessage)
    {
        if (result.IsFailure)
            return ReportFailure(parseResult, result.Error!);

        parseResult.InvocationConfiguration.Output.WriteLine(successMessage());
        return ExitCodes.Success;
    }

    public static int ReportFailure(ParseResult parseResult, Error error)
    {
        parseResult.InvocationConfiguration.Error.WriteLine($"Error: {error.Message}");
        return ExitCodes.ForError(error.Kind);
    }

    /// <summary>The marker appended to a listing line for something the database expects on disk but that is
    /// missing there (FR-1.7.5): the user is told about the mismatch rather than left to find out later.</summary>
    public static string MissingNote(string? missingAt) =>
        missingAt is null ? "" : $"  [NOT FOUND on disk: {missingAt}]";
}
