using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>The one translation of a missing atomic weight into an <see cref="ArgumentException"/> naming the element (F-PR-07).</summary>
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
