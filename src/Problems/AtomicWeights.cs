using APThermo.Data;

namespace APThermo.Problems;

/// <summary>A missing atomic weight as an <see cref="ArgumentException"/> naming the element, for the element check of a chemical
/// system and the mass of a mixture (F-PR-07); a custom reactant's resolution translates the same miss naming the reactant too
/// (<see cref="ReactantResolver"/>).</summary>
internal static class AtomicWeights
{
    public static double Of(SpeciesDatabase database, string element)
    {
        try
        {
            return database.AtomicWeight(element);
        }
        catch (KeyNotFoundException inner)
        {
            throw new ArgumentException($"element '{element}' has no record in the database", inner);
        }
    }
}
