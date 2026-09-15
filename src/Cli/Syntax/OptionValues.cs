using System.Globalization;
using APThermo.Problems;

namespace APThermo.Cli.Syntax;

/// <summary>Parsing of the command-line option values: the number parser shared by --threshold and --mass-tolerance, and the format word.</summary>
internal static class OptionValues
{
    public static double ParseThreshold(string value) => ParseNonNegative(value, "threshold");

    /// <summary>
    /// The predicate is the front door's (<see cref="ElementalMixture.IsValidMassTolerance"/>), so the option and the
    /// library cannot disagree on a valid tolerance (BOOT.md, F-AR-04).
    /// </summary>
    public static double ParseMassTolerance(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !ElementalMixture.IsValidMassTolerance(number))
        {
            throw new InputException($"the mass tolerance must be a finite non-negative number, not '{value}'");
        }

        return number;
    }

    public static OutputFormat ParseFormat(string value) => value switch
    {
        "json" => OutputFormat.Json,
        "csv" => OutputFormat.Csv,
        _ => throw new InputException($"unknown format '{value}'; json or csv"),
    };

    private static double ParseNonNegative(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0.0)
        {
            throw new InputException($"the {name} must be a finite non-negative number, not '{value}'");
        }

        return number;
    }
}
