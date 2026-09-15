using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>
/// One station's figures against the reference: the list of mismatches, empty when every field agrees within the tolerance
/// table. It carries the fields the node computes, the fields that carry the reaction term, and the rule for the stations where
/// the reference is defective — where the reacting fields are skipped and the defect must still be visible (Fixtures BOOT.md).
/// </summary>
internal static class FigureComparison
{
    /// <summary>The station outputs the transport node computes, by fixture name and figure field.</summary>
    public static readonly IReadOnlyList<(string Field, Func<TransportFigures, double> Value)> Figures =
    [
        ("viscosity", f => f.Viscosity),
        ("frozenConductivity", f => f.FrozenConductivity),
        ("reactingConductivity", f => f.ReactingConductivity),
        ("frozenPrandtl", f => f.FrozenPrandtl),
        ("reactingPrandtl", f => f.ReactingPrandtl),
    ];

    /// <summary>Fields that carry the reaction term: skipped where the reference's value is known to be defective.</summary>
    public static readonly IReadOnlySet<string> ReactingFields = new HashSet<string> { "reactingConductivity", "reactingPrandtl" };

    /// <summary>The mismatches of one evaluated station against the reference's fields.</summary>
    public static IReadOnlyList<string> Mismatches(EvaluatedStation station, ToleranceTable tolerances)
    {
        if (station.Evaluation.Status != CaseStatus.Ok)
        {
            return [$"{station.Label}: status {station.Evaluation.Status}"];
        }

        var figures = station.Evaluation.Figures;
        var defective = figures.TraceEliminations > 0;
        var mismatches = new List<string>();
        foreach (var field in Figures)
        {
            var message = Compare(station.Station, field, figures, defective, tolerances);
            if (message is not null)
            {
                mismatches.Add($"{station.Label} {message}");
            }
        }

        return mismatches;
    }

    /// <summary>
    /// One field: its message when the tree and the reference disagree, or, at a defective station, when the reacting
    /// conductivity of the reference has stopped being defective; null when the field is as it should be.
    /// </summary>
    private static string? Compare(JsonElement station, (string Field, Func<TransportFigures, double> Value) field,
                                   TransportFigures figures, bool defective, ToleranceTable tolerances)
    {
        var expected = station.GetProperty(field.Field).GetDouble();
        var actual = field.Value(figures);
        var agrees = tolerances.Matches(field.Field, expected, actual);
        if (defective && ReactingFields.Contains(field.Field))
        {
            // The reference keeps the reaction through the trace species (Fixtures BOOT.md): its reacting conductivity is inflated.
            return field.Field == "reactingConductivity" && agrees
                ? $"{field.Field}: reference {expected:R} agrees with the tree's {actual:R}; the documented defect is gone"
                : null;
        }

        return agrees ? null : $"{field.Field}: reference {expected:R}, tree {actual:R}";
    }
}
