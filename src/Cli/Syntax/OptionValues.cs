using System.Globalization;
using APThermo.Problems;

namespace APThermo.Cli.Syntax;

/// <summary>Parsing of the command-line option values: the number parser shared by --threshold and --mass-tolerance, and the format word.</summary>
internal static class OptionValues
{
    public static double ParseThreshold(string value) => ParseNonNegative(value, "threshold");

    /// <summary>
    /// A path-like option's value, refused when empty (2026-09-26, the audit's finding 9): <c>--output=</c> reached
    /// <see cref="File.WriteAllText(string, string)"/> with an empty path (an unhandled <see cref="ArgumentException"/>,
    /// exit code 3) and <c>--database=</c> silently combined to a bare file name, reading <c>thermo.inp</c> from the
    /// working directory instead of refusing.
    /// </summary>
    public static string ParsePath(string value, string name) =>
        value.Length == 0 ? throw new InputException($"option --{name} needs a non-empty value") : value;

    /// <summary>
    /// The predicate is the front door's (<see cref="ElementalMixture.IsValidMassTolerance"/>), so the option and the
    /// library cannot disagree on a valid tolerance (BOOT.md, F-AR-04).
    /// </summary>
    public static double ParseMassTolerance(string value) =>
        !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !ElementalMixture.IsValidMassTolerance(number)
            ? throw new InputException($"the mass tolerance must be a finite non-negative number, not '{value}'")
            : number;

    public static OutputFormat ParseFormat(string value) => value switch
    {
        "json" => OutputFormat.Json,
        "csv" => OutputFormat.Csv,
        _ => throw new InputException($"unknown format '{value}'; json or csv"),
    };

    private static double ParseNonNegative(string value, string name) =>
        !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0.0
            ? throw new InputException($"the {name} must be a finite non-negative number, not '{value}'")
            : number;
}
