using System.Reflection;
using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>
/// One caveat set applied to one comparison: transport requested, a frozen station, a singular or defective reference
/// (Problems.Tests BOOT.md, invariants). <paramref name="FreezingStationReference"/> is the reference's own freezing
/// station (chamber or throat), used to key the frozen-station cv skip on the reference's defect signature rather than
/// on the <see cref="Frozen"/> flag alone (BOOT.md, 2026-09-26, the guards audit's F5/F6); null when <see cref="Frozen"/>
/// is false or the case has no freezing station (a shifting-equilibrium flow, or a tp/hp/sp problem).
/// </summary>
internal readonly record struct StationCaveats(
    bool Transport, bool Frozen, bool ReferenceDefective = false, bool SingularReference = false, JsonElement? FreezingStationReference = null);

/// <summary>
/// One result station against one fixture station or state, the field list taken from the fixture: state and performance
/// fields by reflection, transport fields from the station's transport figures, mole fractions by name, with
/// <see cref="ReferenceCaveats"/> applied.
/// </summary>
internal static class ReferenceComparison
{
    /// <summary>The mismatches of one result station against one fixture station or state; empty when they agree.</summary>
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

    /// <summary>
    /// The transport fields of the fixture station. At a station whose reference reacting conductivity is defective
    /// (<see cref="StationCaveats.ReferenceDefective"/>, or a trace component the tree's transport pass eliminated), the reacting
    /// fields are not compared, but the defect must still be visible: a reacting conductivity that agrees with the inflated
    /// reference is itself a mismatch.
    /// </summary>
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
            if (caveats.Frozen && ReferenceCaveats.NotAtFrozenStations.Contains(name)
                && IsFrozenCvDefectSignature(expected, caveats.FreezingStationReference, name))
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
        foreach (var entry in moleFractions.EnumerateObject())
        {
            var expected = entry.Value.GetDouble();
            if (!station.MoleFractions.TryGetValue(entry.Name, out var actual))
            {
                yield return $"{label} {entry.Name}: not in the table";
                continue;
            }

            var tolerance = tolerances.MoleFractionField(expected);
            if (!tolerances.Matches(tolerance, expected, actual))
            {
                yield return $"{label} x({entry.Name}): reference {expected:R}, tree {actual:R} [{tolerance}]";
            }
        }
    }

    /// <summary>
    /// The frozen-station cv defect's own signature on the reference (BOOT.md, 2026-09-26, the guards audit's F5/F6): the
    /// reference's <paramref name="name"/> at this frozen station is either exactly zero, or exactly the freezing
    /// station's own recorded value for the same field. Anywhere else the field is compared like any other.
    /// </summary>
    private static bool IsFrozenCvDefectSignature(double expected, JsonElement? freezingStationReference, string name) =>
        expected == 0.0 || (freezingStationReference is { } freeze && expected == freeze.GetProperty(name).GetDouble());

    /// <summary>A numeric station output as a field of MixtureState or PerformanceFigures.</summary>
    private static double FieldValue(Station station, string name)
    {
        var fieldName = char.ToUpperInvariant(name[0]) + name[1..];
        var stateField = typeof(MixtureState).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (stateField is not null)
        {
            return (double)stateField.GetValue(station.State)!;
        }

        var figureField = typeof(PerformanceFigures).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance)
                          ?? throw new InvalidOperationException($"the fixture output {name} has no field in MixtureState or PerformanceFigures");
        return (double)figureField.GetValue(station.Performance ?? throw new InvalidOperationException($"{name} is a performance field but the station has no figures"))!;
    }
}
