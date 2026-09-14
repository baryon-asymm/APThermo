using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>Σ n_i A_i with the database's atomic weights, and the refusal of a mixture beyond its declared tolerance (BOOT.md, invariants).</summary>
internal static class MixtureMass
{
    /// <summary>Kilograms: the mass the element moles describe with the database's atomic weights.</summary>
    public static double Of(SpeciesDatabase database, ElementalMixture mixture)
    {
        var mass = 0.0;
        foreach (var (symbol, moles) in mixture.ElementMoles)
        {
            // Multiplying by the reciprocal (bit-identical to 1.0e-3: both round the exact value 1/1000 to the nearest double)
            // reproduces the pre-decomposition arithmetic exactly; dividing by MolesPerKilomole does not, for a general moles
            // value (proven red at the bit snapshot before this form was chosen).
            mass += moles * (1.0 / UnitFactors.MolesPerKilomole) * AtomicWeights.Of(database, symbol);
        }

        return mass;
    }

    /// <summary>
    /// Element moles are per kilogram: their mass with the database's atomic weights must be one kilogram within the tolerance
    /// the mixture declares (BOOT.md), whichever front door it came through. Returns the mass, which the result reports.
    /// </summary>
    public static double Check(SpeciesDatabase database, ElementalMixture mixture, string subject, int index)
    {
        var mass = Of(database, mixture);
        if (Math.Abs(mass - 1.0) > mixture.MassTolerance)
        {
            throw new MixtureMassException(subject, index, mass, mixture.MassTolerance);
        }

        return mass;
    }
}
