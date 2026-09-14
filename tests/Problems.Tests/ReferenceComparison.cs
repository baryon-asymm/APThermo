using System.Reflection;
using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>One caveat set applied to one comparison: transport requested, a frozen station, a singular or defective reference (Problems.Tests BOOT.md, invariants).</summary>
internal readonly record struct StationCaveats(bool Transport, bool Frozen, bool ReferenceDefective = false, bool SingularReference = false);

/// <summary>
/// One result station against one fixture station or state, the field list taken from the fixture: state and performance
/// fields by reflection, transport fields from the station's transport figures, mole fractions by name, with
/// <see cref="ReferenceCaveats"/> applied.
/// </summary>
internal static class ReferenceComparison
{
    public static IEnumerable<string> Compare(JsonElement reference, Station station, SpeciesList species, string label, ToleranceTable tolerances, StationCaveats caveats)
    {
        var moleFractions = reference.GetProperty("moleFractions");
        var condensedPresent = moleFractions.EnumerateObject().Any(p => p.Value.GetDouble() > 0.0 && species.IsCondensed(p.Name));
        foreach (var mismatch in TransportMismatches(reference, station, label, tolerances, caveats))
        {
            yield return mismatch;
        }

        foreach (var mismatch in StateAndPerformanceMismatches(reference, station, label, tolerances, caveats, condensedPresent))
        {
            yield return mismatch;
        }

        foreach (var mismatch in MoleFractionMismatches(moleFractions, station, label, tolerances))
        {
            yield return mismatch;
        }
    }

    private static IEnumerable<string> TransportMismatches(JsonElement reference, Station station, string label, ToleranceTable tolerances, StationCaveats caveats)
    {
        foreach (var property in reference.EnumerateObject())
        {
            var name = property.Name;
            if (!ReferenceCaveats.TransportFields.TryGetValue(name, out var transportValue) || property.Value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var expected = property.Value.GetDouble();
            if (station.Transport is not { } figures)
            {
                yield return $"{label} {name}: no transport figures (status {station.TransportStatus?.ToString() ?? "not requested"})";
                continue;
            }

            var actualTransport = transportValue(figures);
            if ((caveats.ReferenceDefective || figures.TraceEliminations > 0) && ReferenceCaveats.ReactingFields.Contains(name))
            {
                // The reference keeps the reaction through the trace species (Fixtures BOOT.md): its reacting conductivity is inflated.
                if (name == "reactingConductivity" && tolerances.Matches(name, expected, actualTransport))
                {
                    yield return $"{label} {name}: reference {expected:R} agrees with the tree's {actualTransport:R}; the documented defect is gone";
                }

                continue;
            }

            if (!tolerances.Matches(name, expected, actualTransport))
            {
                yield return $"{label} {name}: reference {expected:R}, tree {actualTransport:R}";
            }
        }
    }

    private static IEnumerable<string> StateAndPerformanceMismatches(JsonElement reference, Station station, string label, ToleranceTable tolerances, StationCaveats caveats, bool condensedPresent)
    {
        foreach (var property in reference.EnumerateObject())
        {
            var name = property.Name;
            if (ReferenceCaveats.Labels.Contains(name) || ReferenceCaveats.TransportFields.ContainsKey(name) || property.Value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var expected = property.Value.GetDouble();
            if (caveats.Frozen && ReferenceCaveats.NotAtFrozenStations.Contains(name))
            {
                continue;
            }

            if (caveats.SingularReference && ReferenceCaveats.SecondOrderFields.Contains(name))
            {
                continue;
            }

            if (caveats.Transport && condensedPresent && ReferenceCaveats.GasPhaseWithTransport.Contains(name))
            {
                if (name == "cpFrozen" && station.Transport is { } set && !tolerances.Matches(name, expected, set.FrozenHeatCapacity))
                {
                    yield return $"{label} {name} (transport set): reference {expected:R}, tree {set.FrozenHeatCapacity:R}";
                }

                continue;
            }

            var actual = FieldValue(station, name);
            if (!tolerances.Matches(name, expected, actual))
            {
                yield return $"{label} {name}: reference {expected:R}, tree {actual:R}";
            }
        }
    }

    private static IEnumerable<string> MoleFractionMismatches(JsonElement moleFractions, Station station, string label, ToleranceTable tolerances)
    {
        var traceThreshold = ReferenceCaveats.TracePrintThreshold(tolerances);
        foreach (var entry in moleFractions.EnumerateObject())
        {
            var expected = entry.Value.GetDouble();
            if (!station.MoleFractions.TryGetValue(entry.Name, out var actual))
            {
                yield return $"{label} {entry.Name}: not in the table";
                continue;
            }

            var tolerance = expected >= traceThreshold ? "moleFraction" : "moleFractionTrace";
            if (!tolerances.Matches(tolerance, expected, actual))
            {
                yield return $"{label} x({entry.Name}): reference {expected:R}, tree {actual:R} [{tolerance}]";
            }
        }
    }

    /// <summary>A numeric station output as a field of MixtureState or PerformanceFigures.</summary>
    private static double FieldValue(Station station, string name)
    {
        var fieldName = char.ToUpperInvariant(name[0]) + name[1..];
        var stateField = typeof(MixtureState).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (stateField is not null)
        {
            return (double)stateField.GetValue(station.State)!;
        }

        var figureField = typeof(PerformanceFigures).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance)
                          ?? throw new InvalidOperationException($"the fixture output {name} has no field in MixtureState or PerformanceFigures");
        return (double)figureField.GetValue(station.Performance ?? throw new InvalidOperationException($"{name} is a performance field but the station has no figures"))!;
    }
}
