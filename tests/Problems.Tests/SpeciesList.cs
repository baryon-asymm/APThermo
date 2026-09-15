namespace APThermo.Problems.Tests;

/// <summary>A result's species list with its gas count: the number of gaseous species at the head, which the table puts first.</summary>
internal readonly record struct SpeciesList(IReadOnlyList<string> Species, int GasCount)
{
    public static SpeciesList Of(IReadOnlyList<string> species) => new(species, GasCountOf(species));

    /// <summary>The number of gaseous species at the head of a result's species list: every name without a phase suffix, which the table puts first.</summary>
    public static int GasCountOf(IReadOnlyList<string> species) => species.TakeWhile(s => !(s.EndsWith(')') && s.Contains('('))).Count();

    /// <summary>Whether <paramref name="name"/> is a condensed species of this list: at or after <see cref="GasCount"/>.</summary>
    public bool IsCondensed(string name)
    {
        for (var j = GasCount; j < Species.Count; j++)
        {
            if (Species[j] == name)
            {
                return true;
            }
        }

        return false;
    }
}
