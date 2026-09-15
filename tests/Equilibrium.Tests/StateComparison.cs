using System.Reflection;
using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>Compares a solution with a fixture's outputs field by field, the field list taken from the fixture.</summary>
internal static class StateComparison
{
    /// <summary>Outputs of the transport node, which this node does not compute.</summary>
    private static readonly HashSet<string> TransportFields =
        ["viscosity", "frozenConductivity", "reactingConductivity", "frozenPrandtl", "reactingPrandtl"];

    /// <summary>Outputs that are not state fields.</summary>
    private static readonly HashSet<string> NonStateFields = ["moleFractions", "converged"];

    /// <summary>The second-order response: skipped where the reference's derivative matrix was singular (the singular-tp defect, Fixtures BOOT.md).</summary>
    private static readonly HashSet<string> SecondOrderFields = ["cpEquilibrium", "cvEquilibrium", "gammaS", "dlnVdlnT", "dlnVdlnP", "soundSpeed"];

    /// <summary>
    /// The state fields of a fixture's outputs, in document order, mapped to the fields of <see cref="MixtureState"/>. An output
    /// without a field is an error for an equilibrium case and is skipped for a rocket station (<paramref name="strict"/> false),
    /// whose other outputs belong to the performance node.
    /// </summary>
    public static IEnumerable<(string Name, double Expected, FieldInfo Field)> StateFields(JsonElement outputs, bool strict = true)
    {
        foreach (var property in outputs.EnumerateObject())
        {
            if (NonStateFields.Contains(property.Name) || TransportFields.Contains(property.Name) || property.Value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var fieldName = char.ToUpperInvariant(property.Name[0]) + property.Name[1..];
            var field = typeof(MixtureState).GetField(fieldName);
            if (field is null)
            {
                if (strict)
                {
                    throw new InvalidOperationException($"the fixture output {property.Name} has no field in MixtureState");
                }

                continue;
            }

            yield return (property.Name, property.Value.GetDouble(), field);
        }
    }

    /// <summary>Every state field and every listed mole fraction outside its tolerance, as messages; empty when the solution matches.</summary>
    public static IReadOnlyList<string> Compare(CeaCase c, HostSolution solution, ToleranceTable tolerances,
                                                Func<string, bool>? includeField = null)
    {
        var mismatches = new List<string>();

        // A tp assigned exactly at a bound two records of one substance share makes the reference's derivative
        // matrix singular, and it prints its convention (cp_eq = 0, gamma_s = -1/dlnVdlnP) instead of derivatives
        // (Fixtures BOOT.md, the singular-tp defect); the tree reports the chosen record's real response. The
        // signature guards the skip: no real tp state has a zero equilibrium heat capacity.
        var singularTp = c.Kind == "tp" && c.Outputs.GetProperty("cpEquilibrium").GetDouble() == 0.0;
        foreach (var (name, expected, field) in StateFields(c.Outputs))
        {
            if (includeField is not null && !includeField(name))
            {
                continue;
            }

            if (singularTp && SecondOrderFields.Contains(name))
            {
                continue;
            }

            var actual = (double)field.GetValue(solution.State)!;
            if (!tolerances.Matches(name, expected, actual))
            {
                mismatches.Add($"{name}: reference {expected:R}, tree {actual:R}");
            }
        }

        foreach (var species in c.Outputs.GetProperty("moleFractions").EnumerateObject())
        {
            var expected = species.Value.GetDouble();
            if (solution.Case.Table.IndicesOf(species.Name).Count == 0)
            {
                mismatches.Add($"{species.Name}: not in the table");
                continue;
            }

            var actual = solution.MoleFraction(species.Name);
            var tolerance = tolerances.MoleFractionField(expected);
            if (!tolerances.Matches(tolerance, expected, actual))
            {
                mismatches.Add($"x({species.Name}): reference {expected:R}, tree {actual:R} [{tolerance}]");
            }
        }

        return mismatches;
    }

    /// <summary>
    /// The condensed species the reference reports present (above the trace threshold) and absent (zero). A species the table
    /// lacks counts as condensed when its name has a phase suffix, so that an omitted candidate is reported missing.
    /// </summary>
    public static (IReadOnlyList<string> Present, IReadOnlyList<string> Absent) CondensedSetOf(CeaCase c, SpeciesTable table, ToleranceTable tolerances)
    {
        var present = new List<string>();
        var absent = new List<string>();
        foreach (var species in c.Outputs.GetProperty("moleFractions").EnumerateObject())
        {
            var indices = table.IndicesOf(species.Name);
            var condensed = indices.Count == 0 ? species.Name.EndsWith(')') && species.Name.Contains('(') : indices[0] >= table.GasCount;
            if (!condensed)
            {
                continue;
            }

            var x = species.Value.GetDouble();
            if (tolerances.MoleFractionField(x) == "moleFraction")
            {
                present.Add(species.Name);
            }
            else if (x == 0.0)
            {
                absent.Add(species.Name);
            }
        }

        return (present, absent);
    }
}
