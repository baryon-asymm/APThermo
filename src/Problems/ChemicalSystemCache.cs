using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>
/// An element list, or a list of mixtures reduced to the union of their elements, plus an Omit/Only selection, mapped to a
/// <see cref="ChemicalSystem"/> built once per key and disposed together with the cache (BOOT.md, batch construction).
/// </summary>
internal sealed class ChemicalSystemCache(SpeciesDatabase database, Engine engine) : IDisposable
{
    private readonly Dictionary<string, ChemicalSystem> _systems = new(StringComparer.Ordinal);

    public ChemicalSystem Get(ElementalMixture mixture) => Get(mixture.Elements, mixture.Omit, mixture.Only);

    /// <summary>The system over the union of the mixtures' elements, in order of first appearance, under the species lists they all share.</summary>
    public ChemicalSystem Union(IReadOnlyList<ElementalMixture> mixtures, int problemCount, string kind)
    {
        if (mixtures.Count != problemCount)
        {
            throw new ArgumentException($"{mixtures.Count} mixtures were given for {problemCount} {kind} problems; a batch over mixtures takes one mixture per problem");
        }

        if (mixtures.Count == 0)
        {
            throw new ArgumentException($"no {kind} problems were given");
        }

        var first = mixtures[0] ?? throw new ArgumentException("mixture 0 is null");
        var elements = new List<string>();
        for (var i = 0; i < mixtures.Count; i++)
        {
            var mixture = mixtures[i] ?? throw new ArgumentException($"mixture {i} is null");
            if (!SameNames(mixture.Omit, first.Omit) || !SameNames(mixture.Only, first.Only))
            {
                throw new ArgumentException($"mixture {i}: its Omit or Only list differs from mixture 0's; a batch has one species selection");
            }

            foreach (var symbol in mixture.Elements)
            {
                if (!elements.Contains(symbol, StringComparer.Ordinal))
                {
                    elements.Add(symbol);
                }
            }
        }

        return Get(elements, first.Omit, first.Only);
    }

    public ChemicalSystem Get(IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only)
    {
        foreach (var element in elements)
        {
            try
            {
                database.AtomicWeight(element);
            }
            catch (KeyNotFoundException inner)
            {
                throw new ArgumentException($"element '{element}' has no record in the database", inner);
            }
        }

        var key = string.Join(",", elements) + "|" + string.Join(",", omit.Order(StringComparer.Ordinal)) + "|" + (only is null ? "*" : string.Join(",", only));
        if (_systems.TryGetValue(key, out var system))
        {
            return system;
        }

        var candidates = SpeciesSelection.Candidates(database, elements, omit, only);
        if (candidates.Count == 0)
        {
            throw new ArgumentException($"no product species of the database consists of the elements {string.Join(", ", elements)} alone");
        }

        var table = SpeciesTable.Build(database, elements.ToList(), candidates);
        var transport = database.Transport is null ? null : TransportTable.Build(database.Transport, table);
        system = new ChemicalSystem(elements.ToList(), table, engine.Upload(table, transport));
        _systems[key] = system;
        return system;
    }

    private static bool SameNames(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null ? b is null : b is not null && a.Count == b.Count && a.ToHashSet(StringComparer.Ordinal).SetEquals(b);

    public void Dispose()
    {
        foreach (var system in _systems.Values)
        {
            system.Dispose();
        }

        _systems.Clear();
    }
}
