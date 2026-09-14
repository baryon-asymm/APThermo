namespace AerospacePropellantThermodynamics.Problems;

/// <summary>
/// The node's unit factors (BOOT.md, units at this boundary): 1000, used both ways as element abundances cross the mol/kg
/// (this node's public unit) and kmol/kg (the numerical nodes') boundary, as a reactant's assigned enthalpy crosses J/mol and
/// J/kmol on its way into the per-kilogram sum, and as a mixture's mass crosses kilograms and grams in the tolerance message.
/// </summary>
internal static class UnitFactors
{
    /// <summary>Moles per kilomole: divide to go from mol to kmol, multiply to go from kmol to mol, or from J/mol to J/kmol.</summary>
    public const double MolesPerKilomole = 1.0e3;

    /// <summary>Grams per kilogram: multiply to go from kg to g.</summary>
    public const double GramsPerKilogram = 1.0e3;
}
