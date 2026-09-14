using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;

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

    private static readonly string[] Header =
    [
        "# The table bits of every fixture case (tests/Thermo.Tests/BOOT.md, the Bits level).",
        "# One line per case: the case file relative to the repository root, then the SHA-256 of the species names",
        "# joined by commas (UTF-8) followed by the eight arrays of SpeciesTableArrays in their declared order",
        "# (MolarMass, FormationEnthalpy, Stoichiometry, IntervalStart, IntervalCount, IntervalBounds, Exponents,",
        "# Coefficients), each value as its little-endian bytes.",
        "# A tripwire, not a contract: a decomposition, a rename or a reordering of code moves no line here.",
    ];

    private readonly Dictionary<string, string> _actual = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _approved = new(StringComparer.Ordinal);

    public BitSnapshot()
    {
        var database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        var lines = new List<string>(Header);
        foreach (var path in CaseFiles())
        {
            var relative = Relative(path);
            _actual[relative] = Digest(database, path);
            lines.Add(relative + " " + _actual[relative]);
        }

        var actualText = string.Join('\n', lines) + "\n";
        NoApprovedFile = !File.Exists(ApprovedPath);
        if (NoApprovedFile)
        {
            File.WriteAllText(ApprovedPath, actualText);
            return;
        }

        var approvedText = File.ReadAllText(ApprovedPath).Replace("\r\n", "\n");
        foreach (var line in approvedText.Split('\n'))
        {
            var space = line.LastIndexOf(' ');
            if (line.Length == 0 || line[0] == '#' || space < 0)
            {
                continue;
            }

            _approved[line[..space]] = line[(space + 1)..];
        }

        if (approvedText == actualText)
        {
            File.Delete(ActualPath);
        }
        else
        {
            File.WriteAllText(ActualPath, actualText);
        }
    }

    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Thermo.Tests", "Bits.approved.txt");

    public static string ActualPath => RepositoryPaths.Resolve("tests", "Thermo.Tests", "Bits.actual.txt");

    /// <summary>True when no approved file existed and one has just been written for review.</summary>
    public bool NoApprovedFile { get; }

    /// <summary>The case files of the kinds that carry a chemical system, enumerated through the fixtures node.</summary>
    public static IEnumerable<string> CaseFiles() => Kinds.SelectMany(FixtureFiles.Enumerate);

    /// <summary>The case file relative to the repository root, with forward slashes: the key of a snapshot line.</summary>
    public static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');

    /// <summary>The bits of the case's table as they are now.</summary>
    public string Current(string relative) => _actual[relative];

    /// <summary>The bits the snapshot records for the case, or null when the snapshot does not know it.</summary>
    public string? Recorded(string relative) => _approved.GetValueOrDefault(relative);

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
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(string.Join(',', table.Species)));
        Append(hash, arrays.MolarMass);
        Append(hash, arrays.FormationEnthalpy);
        Append(hash, arrays.Stoichiometry);
        Append(hash, arrays.IntervalStart);
        Append(hash, arrays.IntervalCount);
        Append(hash, arrays.IntervalBounds);
        Append(hash, arrays.Exponents);
        Append(hash, arrays.Coefficients);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, double[] values)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        foreach (var value in values)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
            hash.AppendData(bytes);
        }
    }

    private static void Append(IncrementalHash hash, int[] values)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            hash.AppendData(bytes);
        }
    }
}
