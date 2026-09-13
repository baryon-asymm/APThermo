using System.Reflection;
using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>
/// Compares a result station with a fixture station or state, the field list taken from the fixture: state and performance
/// fields by reflection, transport fields from the station's transport figures, mole fractions by name, with the caveats of the
/// reference's fields recorded in the Fixtures BOOT.md.
/// </summary>
internal static class Comparison
{
    /// <summary>Below this reference mole fraction the reference lists a species as trace (the derivation of moleFractionTrace in the tolerance table).</summary>
    public const double TracePrintThreshold = 5e-6;

    /// <summary>Station outputs that are neither state, performance nor transport fields.</summary>
    private static readonly HashSet<string> Labels = ["station", "index", "frozen", "moleFractions", "converged"];

    private static readonly IReadOnlyDictionary<string, Func<TransportFigures, double>> TransportFields = new Dictionary<string, Func<TransportFigures, double>>(StringComparer.Ordinal)
    {
        ["viscosity"] = f => f.Viscosity,
        ["frozenConductivity"] = f => f.FrozenConductivity,
        ["reactingConductivity"] = f => f.ReactingConductivity,
        ["frozenPrandtl"] = f => f.FrozenPrandtl,
        ["reactingPrandtl"] = f => f.ReactingPrandtl,
    };

    /// <summary>Fields that carry the reaction term: skipped where the reference's value is known to be defective (Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> ReactingFields = ["reactingConductivity", "reactingPrandtl"];

    /// <summary>The reference computes no Cv at a frozen station (Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> NotAtFrozenStations = ["cvFrozen", "cvEquilibrium"];

    /// <summary>The second-order response: skipped where the reference's derivative matrix was singular (the singular-tp defect, Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> SecondOrderFields = ["cpEquilibrium", "cvEquilibrium", "gammaS", "dlnVdlnT", "dlnVdlnP", "soundSpeed"];

    /// <summary>With transport on, the reference's frozen heat capacities of a station with condensed species are those of the transport set (Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> GasPhaseWithTransport = ["cpFrozen", "cvFrozen"];

    /// <summary>
    /// The mismatches of one result station against one fixture station or state; empty when they agree. At a station whose
    /// reference reacting conductivity is defective (<paramref name="referenceDefective"/>), the reacting fields are not compared
    /// but the defect must still be visible.
    /// </summary>
    /// <summary>
    /// The signature of the singular-tp defect (Fixtures BOOT.md): a tp assigned exactly at a bound two records of one
    /// substance share makes the reference's derivative matrix singular, and it prints its convention (cp_eq = 0,
    /// gamma_s = -1/dlnVdlnP) instead of derivatives. No real tp state has a zero equilibrium heat capacity.
    /// </summary>
    public static bool SingularTp(CeaCase c) => c.Kind == "tp" && c.Outputs.GetProperty("cpEquilibrium").GetDouble() == 0.0;

    public static IEnumerable<string> Compare(JsonElement reference, Station station, IReadOnlyList<string> species, int gasCount,
                                              bool transport, bool frozen, string label, ToleranceTable tolerances, bool referenceDefective = false,
                                              bool singularReference = false)
    {
        var moleFractions = reference.GetProperty("moleFractions");
        var condensedPresent = moleFractions.EnumerateObject().Any(p => p.Value.GetDouble() > 0.0 && IsCondensed(species, gasCount, p.Name));
        foreach (var property in reference.EnumerateObject())
        {
            var name = property.Name;
            if (Labels.Contains(name) || property.Value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var expected = property.Value.GetDouble();
            if (TransportFields.TryGetValue(name, out var transportValue))
            {
                if (station.Transport is not { } figures)
                {
                    yield return $"{label} {name}: no transport figures (status {station.TransportStatus?.ToString() ?? "not requested"})";
                    continue;
                }

                var actualTransport = transportValue(figures);
                if ((referenceDefective || figures.TraceEliminations > 0) && ReactingFields.Contains(name))
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

                continue;
            }

            if (frozen && NotAtFrozenStations.Contains(name))
            {
                continue;
            }

            if (singularReference && SecondOrderFields.Contains(name))
            {
                continue;
            }

            if (transport && condensedPresent && GasPhaseWithTransport.Contains(name))
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

        foreach (var entry in moleFractions.EnumerateObject())
        {
            var expected = entry.Value.GetDouble();
            if (!station.MoleFractions.TryGetValue(entry.Name, out var actual))
            {
                yield return $"{label} {entry.Name}: not in the table";
                continue;
            }

            var tolerance = expected >= TracePrintThreshold ? "moleFraction" : "moleFractionTrace";
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

    private static bool IsCondensed(IReadOnlyList<string> species, int gasCount, string name)
    {
        for (var j = gasCount; j < species.Count; j++)
        {
            if (species[j] == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The number of gaseous species at the head of a result's species list: every name without a phase suffix, which the table puts first.</summary>
    public static int GasCountOf(IReadOnlyList<string> species) => species.TakeWhile(s => !(s.EndsWith(')') && s.Contains('('))).Count();

    /// <summary>Field-by-field bit equality of two stations: state, figures, mole fractions, condensed mass fractions and transport figures.</summary>
    public static IEnumerable<string> BitDifferences(Station expected, Station actual, string label)
    {
        foreach (var difference in BitDifferences(expected.State, actual.State, label + " state"))
        {
            yield return difference;
        }

        if (expected.Performance.HasValue != actual.Performance.HasValue)
        {
            yield return $"{label}: performance figures present on one side only";
        }
        else if (expected.Performance is { } figures)
        {
            foreach (var difference in BitDifferences(figures, actual.Performance!.Value, label + " figures"))
            {
                yield return difference;
            }
        }

        if (expected.Transport.HasValue != actual.Transport.HasValue)
        {
            yield return $"{label}: transport figures present on one side only";
        }
        else if (expected.Transport is { } transport)
        {
            foreach (var difference in BitDifferences(transport, actual.Transport!.Value, label + " transport"))
            {
                yield return difference;
            }
        }

        if (expected.Status != actual.Status || expected.TransportStatus != actual.TransportStatus)
        {
            yield return $"{label}: statuses differ";
        }

        foreach (var (name, x) in expected.MoleFractions)
        {
            if (!actual.MoleFractions.TryGetValue(name, out var y) || !SameBits(x, y))
            {
                yield return $"{label} x({name}): {x:R} vs {(actual.MoleFractions.TryGetValue(name, out var v) ? v.ToString("R") : "missing")}";
            }
        }

        foreach (var (name, x) in expected.CondensedMassFractions)
        {
            if (!actual.CondensedMassFractions.TryGetValue(name, out var y) || !SameBits(x, y))
            {
                yield return $"{label} w({name}) differs";
            }
        }
    }

    public static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);


    /// <summary>
    /// Two stations within a relative tolerance on every double field (state, figures, transport), mole fractions above the floor
    /// included, and exactly on statuses and counts; for a batch whose element order changed the pivot order of the linear solves.
    /// </summary>
    public static IEnumerable<string> RelativeDifferences(Station expected, Station actual, double relative, double moleFractionFloor, string label)
    {
        static bool Close(double p, double q, double relative) => Math.Abs(p - q) <= relative * Math.Max(Math.Abs(p), Math.Abs(q));

        if (expected.Status != actual.Status || expected.TransportStatus != actual.TransportStatus)
        {
            yield return $"{label}: statuses differ";
        }

        foreach (var field in typeof(MixtureState).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var p = (double)field.GetValue(expected.State)!;
            var q = (double)field.GetValue(actual.State)!;
            if (!Close(p, q, relative))
            {
                yield return $"{label} state {field.Name}: {p:R} vs {q:R}";
            }
        }

        if (expected.Performance.HasValue != actual.Performance.HasValue)
        {
            yield return $"{label}: performance figures present on one side only";
        }
        else if (expected.Performance is { } figures)
        {
            foreach (var field in typeof(PerformanceFigures).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var p = (double)field.GetValue(figures)!;
                var q = (double)field.GetValue(actual.Performance!.Value)!;
                if (!Close(p, q, relative))
                {
                    yield return $"{label} figures {field.Name}: {p:R} vs {q:R}";
                }
            }
        }

        foreach (var difference in TransportDifferences(expected, actual, relative, label))
        {
            yield return difference;
        }

        foreach (var (name, p) in expected.MoleFractions)
        {
            var q = actual.MoleFractions.GetValueOrDefault(name);
            if (Math.Max(p, q) >= moleFractionFloor && !Close(p, q, relative))
            {
                yield return $"{label} x({name}): {p:R} vs {q:R}";
            }
        }
    }

    /// <summary>The transport figures of two stations within a relative tolerance on the doubles and exactly on the counts and statuses.</summary>
    public static IEnumerable<string> TransportDifferences(Station expected, Station actual, double relative, string label)
    {
        if (expected.TransportStatus != actual.TransportStatus || expected.Transport.HasValue != actual.Transport.HasValue)
        {
            yield return $"{label}: transport statuses differ";
            yield break;
        }

        if (expected.Transport is not { } a || actual.Transport is not { } b)
        {
            yield break;
        }

        foreach (var field in typeof(TransportFigures).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var x = field.GetValue(a)!;
            var y = field.GetValue(b)!;
            var same = x is double p && y is double q ? Math.Abs(p - q) <= relative * Math.Max(Math.Abs(p), Math.Abs(q)) : x.Equals(y);
            if (!same)
            {
                yield return $"{label} transport {field.Name}: {x} vs {y}";
            }
        }
    }

    private static IEnumerable<string> BitDifferences<T>(T expected, T actual, string label) where T : struct
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var a = field.GetValue(expected)!;
            var b = field.GetValue(actual)!;
            var same = a is double x && b is double y ? SameBits(x, y) : a.Equals(b);
            if (!same)
            {
                yield return $"{label} {field.Name}: {a} vs {b}";
            }
        }
    }
}
