using System.Globalization;
using System.Text;
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

        // Validated before the cache lookup (2026-09-28, the third audit pass, finding 3): the key below carries
        // only the elements, Omit and Only, never a call's own abundances, so a system cached for one call's
        // (harmless, zero-abundance) element must not be handed, unvalidated, to a later call whose own mixture
        // gives that element a real amount. The candidate list is recomputed on every call, a cache hit included,
        // the same cost ValidateOwnElements already pays once per record of a batch.
        var candidates = ValidatedCandidates(elements, omit, only, elementsWithAbundance);
        var key = KeyOf(elements, omit, only);
        if (_systems.TryGetValue(key, out var system))
        {
            return system;
        }

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

    private static string KeyOf(IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only)
    {
        var key = new StringBuilder();
        AppendNames(key, elements);
        AppendNames(key, [.. omit.Order(StringComparer.Ordinal)]);
        AppendNames(key, only);
        return key.ToString();
    }

    /// <summary>Length framing preserves arbitrary UTF-16 names and sequence/null boundaries.</summary>
    private static void AppendNames(StringBuilder key, IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            _ = key.Append("-1:");
            return;
        }

        _ = key.Append(names.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var name in names)
        {
            _ = key.Append((name?.Length ?? -1).ToString(CultureInfo.InvariantCulture)).Append(':');
            _ = key.Append(name);
        }
    }

    private static bool SameNames(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null ? b is null : b is not null && a.Count == b.Count && a.ToHashSet(StringComparer.Ordinal).SetEquals(b);

    public void Dispose()
    {
        ChemicalSystem? firstFailure = null;
        ChemicalSystem? lastFailure = null;
        try
        {
            foreach (var system in _systems.Values)
            {
                try
                {
                    system.Dispose();
                }
                catch (Exception error) when (error is not AccessViolationException)
                {
                    RecordFailure(ref firstFailure, ref lastFailure, system, error);
                }
            }
        }
        finally
        {
            _systems.Clear();
        }

        if (firstFailure is not null)
        {
            throw new AggregateException("chemical-system cleanup failed", DisposalFailures(firstFailure));
        }
    }

    private static void RecordFailure(ref ChemicalSystem? first, ref ChemicalSystem? last, ChemicalSystem system, Exception error)
    {
        system.DisposalFailure = error;
        if (last is not null)
        {
            last.NextFailedDisposal = system;
        }
        else
        {
            first = system;
        }

        last = system;
    }

    private static IEnumerable<Exception> DisposalFailures(ChemicalSystem first)
    {
        for (var current = first; current is not null; current = current.NextFailedDisposal)
        {
            yield return current.DisposalFailure!;
        }
    }
}
