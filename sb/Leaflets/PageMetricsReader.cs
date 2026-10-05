using System.Globalization;
using System.Text.RegularExpressions;

namespace sb.Leaflets;

/// <summary>
/// Parses a document template's `geometry` package margins and `\fontsize` command into a
/// <see cref="PageMetrics"/> (FR-026, research.md §14). Paper height is not parsed — the project
/// always uses A4 (confirmed 2026-09-27) — only the margins/fontsize, which can plausibly change
/// between templates, are read.
/// </summary>
public static class PageMetricsReader
{
    private const double PtPerInch = 72.27;
    private const double MmPerInch = 25.4;
    private const double A4HeightMm = 297.0;
    private static readonly double A4HeightPt = A4HeightMm * PtPerInch / MmPerInch;

    private static readonly Regex GeometryOptionsPattern = new(@"\\usepackage\[([^\]]*)\]\{geometry\}");
    private static readonly Regex FontSizePattern = new(@"\\fontsize\{[0-9.]+pt\}\{([0-9.]+)pt\}");
    private static readonly Regex LengthPattern = new(@"^([0-9.]+)(in|pt)$");

    public static PageMetrics Read(string rawTemplate, string templatePath)
    {
        var geometryMatch = GeometryOptionsPattern.Match(rawTemplate);
        if (!geometryMatch.Success)
        {
            throw new InvalidOperationException(
                $"'{templatePath}' is missing its expected `\\usepackage[...]{{geometry}}` margin declaration.");
        }

        var options = ParseOptions(geometryMatch.Groups[1].Value);
        var topMarginPt = ResolveMarginPt(options, "tmargin", templatePath);
        var bottomMarginPt = ResolveMarginPt(options, "bmargin", templatePath);

        var fontSizeMatch = FontSizePattern.Match(rawTemplate);
        if (!fontSizeMatch.Success)
        {
            throw new InvalidOperationException(
                $"'{templatePath}' is missing its expected `\\fontsize{{size}}{{baselineskip}}` command.");
        }

        var baselineSkipPt = double.Parse(fontSizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        return new PageMetrics(A4HeightPt, topMarginPt, bottomMarginPt, baselineSkipPt);
    }

    private static Dictionary<string, string> ParseOptions(string optionsText)
    {
        var options = new Dictionary<string, string>();
        foreach (var rawOption in optionsText.Split(','))
        {
            var option = rawOption.Trim();
            var equalsIndex = option.IndexOf('=');
            if (equalsIndex < 0)
                continue; // a flag option with no value (e.g. "a4paper") — not a margin key

            options[option[..equalsIndex].Trim()] = option[(equalsIndex + 1)..].Trim();
        }

        return options;
    }

    private static double ResolveMarginPt(IReadOnlyDictionary<string, string> options, string key, string templatePath)
    {
        if (options.TryGetValue(key, out var value) || options.TryGetValue("margin", out value))
            return ParseLengthPt(value, templatePath);

        throw new InvalidOperationException(
            $"'{templatePath}' is missing a `{key}` (or fallback `margin`) option in its `geometry` package declaration.");
    }

    private static double ParseLengthPt(string value, string templatePath)
    {
        var match = LengthPattern.Match(value);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"'{templatePath}' has an unrecognized geometry length '{value}' (only 'in'/'pt' units are supported).");
        }

        var amount = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return match.Groups[2].Value switch
        {
            "in" => amount * PtPerInch,
            "pt" => amount,
            _ => throw new InvalidOperationException($"Unsupported geometry unit in '{value}'.")
        };
    }
}
