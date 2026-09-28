using APThermo.Data;
using APThermo.Execution;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems;

/// <summary>
/// An element list, or a list of mixtures reduced to the union of their elements, plus an Omit/Only selection, mapped to a
/// <see cref="ChemicalSystem"/> built once per key and disposed together with the cache (BOOT.md, batch construction).
/// </summary>
internal sealed class ChemicalSystemCache(SpeciesDatabase database, Engine engine) : IDisposable
{
    /// <summary>Separates the items within one of the three lists the key joins; no species name of the committed
    /// database contains it (the printable-ASCII scan behind the second audit's observation 4), unlike the comma
    /// the previous key used, which 84 product names contain.</summary>
    private const char KeyItemSeparator = '\u001f';

    /// <summary>Separates the three lists (elements, Omit, Only) within the key; equally absent from every name.</summary>
    private const char KeyFieldSeparator = '\u0000';

    private readonly Dictionary<string, ChemicalSystem> _systems = new(StringComparer.Ordinal);

    public ChemicalSystem Get(ElementalMixture mixture) => Get(mixture.Elements, mixture.Omit, mixture.Only, AbundantElementsOf(mixture));

    /// <summary>
    /// Every check <see cref="Get(IReadOnlyList{string}, IReadOnlyList{string}, IReadOnlyList{string}?, IReadOnlyCollection{string})"/>
    /// applies to a system's elements, run over <paramref name="mixture"/>'s own elements and abundances alone, before a
    /// state batch's records are folded into one union (BOOT.md, the second audit's fixes: "a record is named for its
    /// own elements"). Builds no <see cref="ChemicalSystem"/> and touches no accelerator: only the atomic-weight and
    /// candidate checks run, so a batch of many records with one shared element set pays for them once per record
    /// instead of once per table.
    /// </summary>
    public void ValidateOwnElements(ElementalMixture mixture)
    {
        foreach (var element in mixture.Elements)
        {
            _ = AtomicWeights.Of(database, element);
        }

        _ = ValidatedCandidates(mixture.Elements, mixture.Omit, mixture.Only, AbundantElementsOf(mixture));
    }

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
        var abundant = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < mixtures.Count; i++)
        {
            var mixture = mixtures[i] ?? throw new ArgumentException($"mixture {i} is null");
            if (!SameNames(mixture.Omit, first.Omit) || !SameNames(mixture.Only, first.Only))
            {
                throw new ArgumentException($"mixture {i}: its Omit or Only list differs from mixture 0's; a batch has one species selection");
            }

            abundant.UnionWith(AbundantElementsOf(mixture));
        }

        var elements = ElementOrder.OfFirstAppearance(mixtures.Select(mixture => mixture.Elements));
        return Get(elements, first.Omit, first.Only, abundant);
    }

    public ChemicalSystem Get(IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only, IReadOnlyCollection<string> elementsWithAbundance)
    {
        foreach (var element in elements)
        {
            _ = AtomicWeights.Of(database, element);
        }

        var key = KeyOf(elements, omit, only);
        if (_systems.TryGetValue(key, out var system))
        {
            return system;
        }

        var candidates = ValidatedCandidates(elements, omit, only, elementsWithAbundance);
        var table = SpeciesTable.Build(database, [.. elements], candidates);
        var transport = database.Transport is null ? null : TransportTable.Build(database.Transport, table);
        system = new ChemicalSystem([.. elements], table, engine.Upload(table, transport));
        _systems[key] = system;
        return system;
    }

    /// <summary>The candidate species of <paramref name="elements"/>, refusing an empty set or an element with none among them that carries an abundance somewhere.</summary>
    private IReadOnlyList<string> ValidatedCandidates(IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only, IReadOnlyCollection<string> elementsWithAbundance)
    {
        var candidates = SpeciesSelection.Candidates(database, elements, omit, only);
        if (candidates.Count == 0)
        {
            throw new ArgumentException($"no product species of the database consists of the elements {string.Join(", ", elements)} alone");
        }

        SpeciesSelection.ValidateElementsHaveCandidates(database, elements, candidates, omit, only, elementsWithAbundance);
        return candidates;
    }

    /// <summary>The elements of <paramref name="mixture"/> whose abundance is not zero (BOOT.md, the second audit's fixes: an element at zero everywhere is masked, not refused).</summary>
    private static HashSet<string> AbundantElementsOf(ElementalMixture mixture) =>
        new(mixture.ElementMoles.Where(pair => pair.Value > 0.0).Select(pair => pair.Key), StringComparer.Ordinal);

    private static string KeyOf(IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only) =>
        string.Join(KeyItemSeparator, elements) + KeyFieldSeparator +
        string.Join(KeyItemSeparator, omit.Order(StringComparer.Ordinal)) + KeyFieldSeparator +
        (only is null ? "*" : string.Join(KeyItemSeparator, only));

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
