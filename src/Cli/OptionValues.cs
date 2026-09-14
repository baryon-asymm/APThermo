using System.Globalization;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Parsing of the command-line option values: the number parser shared by --threshold and --mass-tolerance, and the format word.</summary>
internal static class OptionValues
{
    public static double ParseThreshold(string value) => ParseNonNegative(value, "threshold");

    /// <summary>Validated a second time by <see cref="Problems.ElementalMixture.IsValidMassTolerance"/> where the front door builds the mixture.</summary>
    public static double ParseMassTolerance(string value) => ParseNonNegative(value, "mass tolerance");

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
