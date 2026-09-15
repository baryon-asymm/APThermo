using System.Reflection;
using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Compares the solver's stations with a rocket fixture's, the field lists taken from the fixture and the result structs.</summary>
internal static class StationComparison
{
    /// <summary>Outputs of the transport node, which this node does not compute.</summary>
    private static readonly HashSet<string> TransportFields =
        ["viscosity", "frozenConductivity", "reactingConductivity", "frozenPrandtl", "reactingPrandtl"];

    /// <summary>Station outputs that are neither state nor performance fields.</summary>
    private static readonly HashSet<string> Labels = ["station", "index", "frozen", "moleFractions"];

    /// <summary>The reference computes no Cv at a frozen station (Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> NotAtFrozenStations = ["cvFrozen", "cvEquilibrium"];

    /// <summary>With transport on, the reference's frozen heat capacities of a station with condensed species are gas-phase values (Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> GasPhaseWithTransport = ["cpFrozen", "cvFrozen"];

    /// <summary>Every numeric station output mapped to a field of <see cref="MixtureState"/> or <see cref="PerformanceFigures"/>.</summary>
    public static IEnumerable<(string Name, double Expected, FieldInfo Field, bool IsFigure)> Fields(JsonElement station)
    {
        foreach (var property in station.EnumerateObject())
        {
            if (Labels.Contains(property.Name) || TransportFields.Contains(property.Name) || property.Value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var fieldName = char.ToUpperInvariant(property.Name[0]) + property.Name[1..];
            var stateField = typeof(MixtureState).GetField(fieldName);
            if (stateField is not null)
            {
                yield return (property.Name, property.Value.GetDouble(), stateField, false);
                continue;
            }

            var figureField = typeof(PerformanceFigures).GetField(fieldName)
                              ?? throw new InvalidOperationException($"the station output {property.Name} has no field in MixtureState or PerformanceFigures");
            yield return (property.Name, property.Value.GetDouble(), figureField, true);
        }
    }

    /// <summary>Mismatches of every solver station against its fixture station; empty when the case matches.</summary>
    public static IReadOnlyList<string> Compare(CeaCase c, RocketSolution solution, ToleranceTable tolerances)
    {
        var mismatches = new List<string>();
        var referenceStations = RocketInputs.FixtureStationsOf(c);
        if (referenceStations.Count != solution.StationCount)
        {
            mismatches.Add($"station count: reference {referenceStations.Count}, tree {solution.StationCount}");
            return mismatches;
        }

        var transport = c.Inputs.GetProperty("transport").GetBoolean();
        for (var s = 0; s < solution.StationCount; s++)
        {
            var reference = referenceStations[s];
            var label = reference.GetProperty("station").GetString();
            var frozen = reference.GetProperty("frozen").GetBoolean();
            var moleFractions = reference.GetProperty("moleFractions");
            var condensedPresent = moleFractions.EnumerateObject().Any(p => p.Value.GetDouble() > 0.0 && IsCondensed(solution.Table, p.Name));
            foreach (var (name, expected, field, isFigure) in Fields(reference))
            {
                if (frozen && NotAtFrozenStations.Contains(name))
                {
                    continue;
                }

                if (transport && condensedPresent && GasPhaseWithTransport.Contains(name))
                {
                    continue;
                }

                var actual = isFigure ? (double)field.GetValue(solution.Outcome.Figures[s])! : (double)field.GetValue(solution.Outcome.Stations[s])!;
                if (!tolerances.Matches(name, expected, actual))
                {
                    mismatches.Add($"{label} {name}: reference {expected:R}, tree {actual:R}");
                }
            }

            foreach (var species in moleFractions.EnumerateObject())
            {
                var expected = species.Value.GetDouble();
                if (solution.Table.IndicesOf(species.Name).Count == 0)
                {
                    mismatches.Add($"{label} {species.Name}: not in the table");
                    continue;
                }

                var actual = solution.MoleFraction(s, species.Name);
                var tolerance = tolerances.MoleFractionField(expected);
                if (!tolerances.Matches(tolerance, expected, actual))
                {
                    mismatches.Add($"{label} x({species.Name}): reference {expected:R}, tree {actual:R} [{tolerance}]");
                }
            }
        }

        return mismatches;
    }

    private static bool IsCondensed(SpeciesTable table, string species)
    {
        var indices = table.IndicesOf(species);
        return indices.Count > 0 && indices[0] >= table.GasCount;
    }
}
