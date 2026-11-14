using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Thermo.Tests;

/// <summary>
/// The table bits of every fixture case, computed once per test class: the case file relative to the repository root
/// and the SHA-256 of the species names and the eight arrays of <see cref="SpeciesTableArrays"/>. No accelerator is
/// created: the bits are those of the host arrays, so no runtime enters the snapshot (BOOT.md, the Bits level).
/// Held by <see cref="BitSnapshotTests"/> as a <c>private static readonly</c> field, not through
/// <c>IClassFixture&lt;T&gt;</c>, which would force this internal-only helper public for no reason a consumer
/// outside this node has (CA1515).
/// </summary>
internal sealed class BitSnapshot
{
    /// <summary>The kinds whose cases carry a chemical system (element moles and a product list), in the snapshot's order.</summary>
    private static readonly string[] Kinds = ["tp", "hp", "sp", "rocket"];

    private readonly ApprovedSnapshot _approved = ApprovedSnapshot.Load(ApprovedPath);
    private readonly Dictionary<string, string?> _problems = new(StringComparer.Ordinal);

    public BitSnapshot()
    {
        var database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        foreach (var path in CaseFiles())
        {
            var relative = Relative(path);
            var (hash, fields) = Digest(database, path);
            _problems[relative] = _approved.Problem(relative, hash, fields);
        }
    }

    public static string ApprovedPath => ApprovedSnapshot.RecordPathFor(RepositoryPaths.Resolve("tests", "Thermo.Tests"), "Bits");

    /// <summary>The case files of the kinds that carry a chemical system, enumerated through the fixtures node.</summary>
    public static IEnumerable<string> CaseFiles() => Kinds.SelectMany(FixtureFiles.Enumerate);

    /// <summary>The case file relative to the repository root, with forward slashes: the key of a snapshot line.</summary>
    public static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');

    /// <summary>Null when the case's table gives the recorded bits; otherwise the problem, naming the case and how to approve.</summary>
    public string? Problem(string relative) => _problems[relative];

    /// <summary>The approved keys no case file of the enumerated kinds produced: a deleted or renamed fixture, left behind in the snapshot.</summary>
    public IReadOnlyList<string> StaleKeys() => _approved.StaleKeys(_problems.Keys);

    /// <summary>The table of one case: the elements are the names of its element moles in their order, the species its product list.</summary>
    private static (string Hash, IReadOnlyList<string> Fields) Digest(SpeciesDatabase database, string path)
    {
        var inputs = CeaFixtures.Load(path).Inputs;
        var elements = inputs.GetProperty("elementMoles").EnumerateObject().Select(property => property.Name).ToArray();
        var species = inputs.GetProperty("products").EnumerateArray().Select(value => value.GetString()!).ToArray();
        SpeciesTable table;
        try
        {
            table = SpeciesTable.Build(database, elements, species);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"the table of {Relative(path)} does not build: {e.Message}", e);
        }

        var arrays = table.Arrays;
        var hash = new BitHash()
            .Add(string.Join(',', table.Species))
            .Add(arrays.MolarMass)
            .Add(arrays.FormationEnthalpy)
            .Add(arrays.Stoichiometry)
            .Add(arrays.IntervalStart)
            .Add(arrays.IntervalCount)
            .Add(arrays.IntervalBounds)
            .Add(arrays.Exponents)
            .Add(arrays.Coefficients);
        var labels = new List<string> { "Species" };
        labels.AddRange(table.Species.Select(name => $"MolarMass[{name}]"));
        labels.AddRange(table.Species.Select(name => $"FormationEnthalpy[{name}]"));
        labels.AddRange(table.Elements.SelectMany(element => table.Species.Select(name => $"Stoichiometry[{element},{name}]")));
        labels.AddRange(table.Species.Select(name => $"IntervalStart[{name}]"));
        labels.AddRange(table.Species.Select(name => $"IntervalCount[{name}]"));
        var owners = IntervalOwners(table);
        labels.AddRange(Enumerable.Range(0, arrays.IntervalBounds.Length).Select(index => $"IntervalBounds[{owners[index / 2]},{(index % 2 == 0 ? "TLow" : "THigh")}]"));
        labels.AddRange(Enumerable.Range(0, arrays.Exponents.Length).Select(index => $"Exponents[{owners[index / 8]},{index % 8}]"));
        labels.AddRange(Enumerable.Range(0, arrays.Coefficients.Length).Select(index => $"Coefficients[{owners[index / 9]},{index % 9}]"));
        Assert.Equal(labels.Count, hash.Fields.Count);
        return (hash.ToHex(), labels.Zip(hash.Fields, (label, value) => $"{label}={value}").ToArray());
    }

    private static string[] IntervalOwners(SpeciesTable table)
    {
        var owners = new string[table.Arrays.IntervalTotal];
        for (var species = 0; species < table.SpeciesCount; species++)
        {
            var start = table.Arrays.IntervalStart[species];
            for (var piece = 0; piece < table.Arrays.IntervalCount[species]; piece++)
            {
                owners[start + piece] = $"{table.Species[species]},piece={piece}";
            }
        }

        return owners;
    }
}
