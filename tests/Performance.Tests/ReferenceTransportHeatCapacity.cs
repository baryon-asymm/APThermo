using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Independently reconstructs the reference's gas-only frozen heat capacity from its composition and NASA fits.</summary>
internal static class ReferenceTransportHeatCapacity
{
    public static bool HasGasPhaseSignature(JsonElement reference, SpeciesTable table, string name, double expected,
                                            ToleranceTable tolerances)
    {
        var temperature = reference.GetProperty("temperature").GetDouble();
        var weightedCp = 0.0;
        var weightedMass = 0.0;
        foreach (var species in reference.GetProperty("moleFractions").EnumerateObject())
        {
            var indices = table.IndicesOf(species.Name);
            if (indices.Count == 0 || indices[0] >= table.GasCount)
            {
                continue;
            }

            var index = indices[0];
            var fraction = species.Value.GetDouble();
            weightedCp += fraction * CpOverR(table.Arrays, index, temperature);
            weightedMass += fraction * table.Arrays.MolarMass[index];
        }

        if (!(weightedMass > 0.0))
        {
            return false;
        }

        var gasCp = PhysicalConstants.R * weightedCp / weightedMass;
        var reconstructed = name == "cpFrozen" ? gasCp : gasCp - PhysicalConstants.R / reference.GetProperty("molarMass").GetDouble();
        return tolerances.Matches(name, expected, reconstructed);
    }

    private static double CpOverR(SpeciesTableArrays arrays, int species, double temperature)
    {
        var start = arrays.IntervalStart[species];
        var count = arrays.IntervalCount[species];
        var interval = start + count - 1;
        for (var i = 0; i < count - 1; i++)
        {
            if (temperature <= arrays.IntervalBounds[(start + i) * 2 + 1])
            {
                interval = start + i;
                break;
            }
        }

        var cp = 0.0;
        for (var i = 0; i < 7; i++)
        {
            cp += arrays.Coefficients[interval * 9 + i] * Math.Pow(temperature, arrays.Exponents[interval * 8 + i]);
        }

        return cp;
    }
}
