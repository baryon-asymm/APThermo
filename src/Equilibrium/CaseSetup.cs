using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// What a case needs before the first Newton step: the element and species marks, and the initial estimate. Kernel-compatible.
/// </summary>
internal static class CaseSetup
{
    /// <summary>The mark of a species in the scratch (the domain is in the node's API.md).</summary>
    public static SpeciesMark Mark(in EquilibriumScratch scratch, int species) => (SpeciesMark)scratch.SpeciesActive[species];

    /// <summary>Writes a species' mark.</summary>
    public static void Mark(in EquilibriumScratch scratch, int species, SpeciesMark mark) => scratch.SpeciesActive[species] = (int)mark;

    /// <summary>
    /// Whether the species takes part in this case at all: its elements are present and the anti-cycling rule has not stood
    /// it down. A record forgiven once is still in play — it may be skipped by one inclusion pass, not removed from the case.
    /// </summary>
    public static bool InPlay(in EquilibriumScratch scratch, int species)
    {
        var mark = Mark(scratch, species);
        return mark == SpeciesMark.Active || mark == SpeciesMark.ForgivenOnce;
    }
}
