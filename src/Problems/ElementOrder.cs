namespace AerospacePropellantThermodynamics.Problems;

/// <summary>The element order of a chemical system (BOOT.md, Constraints): the order of first appearance across a sequence of symbol lists.</summary>
internal static class ElementOrder
{
    /// <summary>Every distinct symbol of <paramref name="symbolLists"/>, in the order it is first seen, a list's own order preserved.</summary>
    public static IReadOnlyList<string> OfFirstAppearance(IEnumerable<IEnumerable<string>> symbolLists)
    {
        var elements = new List<string>();
        foreach (var symbols in symbolLists)
        {
            foreach (var symbol in symbols)
            {
                if (!elements.Contains(symbol, StringComparer.Ordinal))
                {
                    elements.Add(symbol);
                }
            }
        }

        return elements;
    }
}
