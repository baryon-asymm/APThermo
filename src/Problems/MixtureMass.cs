using APThermo.Data;

namespace APThermo.Problems;

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

    /// <summary>The subject a mass-check exception names, stated once for both runners: the propellant's own indexed mixture, or the caller's noun ("mixture", "state record") at the case index, when there is no propellant.</summary>
    public static string Subject(Propellant? propellant, string noun, int index) =>
        propellant is null ? $"{noun} {index}" : $"the propellant's mixture (case {index})";

    /// <summary>
    /// Element moles are per kilogram: their mass with the database's atomic weights must be one kilogram within the tolerance
    /// the mixture declares (BOOT.md), whichever front door it came through. A propellant fails this only when a reactant's
    /// molar mass contradicts its formula by more than the tolerance: a record's (the committed file's ADN) or the MolarMass a
    /// custom definition gives. Returns the mass, which the result reports.
    /// </summary>
    public static double Check(SpeciesDatabase database, ElementalMixture mixture, string subject, int index)
    {
        var mass = Of(database, mixture);
        return Math.Abs(mass - 1.0) > mixture.MassTolerance
            ? throw new MixtureMassException(subject, index, mass, mixture.MassTolerance)
            : mass;
    }
}
