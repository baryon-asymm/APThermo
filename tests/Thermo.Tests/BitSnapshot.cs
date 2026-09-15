using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>
/// The table bits of every fixture case, computed once per test class: the case file relative to the repository root
/// and the SHA-256 of the species names and the eight arrays of <see cref="SpeciesTableArrays"/>. No accelerator is
/// created: the bits are those of the host arrays, so no runtime enters the snapshot (BOOT.md, the Bits level).
/// </summary>
public sealed class BitSnapshot
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
            _problems[relative] = _approved.Problem(relative, Digest(database, path));
        }
    }

    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Thermo.Tests", "Bits.approved.txt");

    /// <summary>The case files of the kinds that carry a chemical system, enumerated through the fixtures node.</summary>
    public static IEnumerable<string> CaseFiles() => Kinds.SelectMany(FixtureFiles.Enumerate);

    /// <summary>The case file relative to the repository root, with forward slashes: the key of a snapshot line.</summary>
    public static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');

    /// <summary>Null when the case's table gives the recorded bits; otherwise the problem, naming the case and how to approve.</summary>
    public string? Problem(string relative) => _problems[relative];

    /// <summary>The approved keys no case file of the enumerated kinds produced: a deleted or renamed fixture, left behind in the snapshot.</summary>
    public IReadOnlyList<string> StaleKeys() => _approved.StaleKeys(_problems.Keys);

    /// <summary>The table of one case: the elements are the names of its element moles in their order, the species its product list.</summary>
    private static string Digest(SpeciesDatabase database, string path)
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
        return new BitHash()
            .Add(string.Join(',', table.Species))
            .Add(arrays.MolarMass)
            .Add(arrays.FormationEnthalpy)
            .Add(arrays.Stoichiometry)
            .Add(arrays.IntervalStart)
            .Add(arrays.IntervalCount)
            .Add(arrays.IntervalBounds)
            .Add(arrays.Exponents)
            .Add(arrays.Coefficients)
            .ToHex();
    }
}
