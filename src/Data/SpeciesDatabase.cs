using System.Security.Cryptography;
using System.Text;

namespace APThermo.Data;

/// <summary>The NASA thermodynamic database (and optionally the transport database) as an immutable object model.</summary>
public sealed class SpeciesDatabase
{
    private readonly Dictionary<string, List<Species>> _recordsByName;
    private readonly Dictionary<string, double> _atomicWeights;

    private SpeciesDatabase(ThermoFile.Result thermo, TransportDatabase? transport, string thermoSha256, string? transSha256)
    {
        Products = thermo.Products;
        Reactants = thermo.Reactants;
        Transport = transport;
        Provenance = new DatabaseProvenance(thermo.HeaderDate, thermo.DefaultIntervalBounds, thermoSha256, transSha256);
        _recordsByName = IndexByName(thermo.Products, thermo.Reactants);
        _atomicWeights = BuildAtomicWeights(thermo.Products);
    }

    /// <summary>Species of the PRODUCTS section, in file order.</summary>
    public IReadOnlyList<Species> Products { get; }

    /// <summary>Records of the REACTANTS section, in file order.</summary>
    public IReadOnlyList<Species> Reactants { get; }

    /// <summary>The transport database, or null when no <c>trans.inp</c> was given.</summary>
    public TransportDatabase? Transport { get; }

    public DatabaseProvenance Provenance { get; }

    /// <summary>Exact, case-sensitive name lookup; products are searched before reactants.</summary>
    public Species this[string name] =>
        TryGet(name, out var species) ? species : throw new KeyNotFoundException($"species '{name}' is not in the database");

    public bool TryGet(string name, out Species species)
    {
        if (_recordsByName.TryGetValue(name, out var records))
        {
            species = records[0];
            return true;
        }

        species = null!;
        return false;
    }

    /// <summary>Every record of the exact name, in file order, products before reactants; empty when the name is unknown.</summary>
    public IReadOnlyList<Species> Records(string name) => _recordsByName.TryGetValue(name, out var records) ? records : [];

    /// <summary>
    /// The atomic weight of an element in kg/kmol: the molar mass of the monatomic gaseous product species with that
    /// symbol, built once at load. Symbols are matched ignoring case, so both "AL" (the formula spelling) and "Al" work.
    /// </summary>
    public double AtomicWeight(string element) =>
        _atomicWeights.TryGetValue(element, out var weight)
            ? weight
            : throw new KeyNotFoundException($"no monatomic gaseous species for element '{element}'; its atomic weight is unknown");

    /// <summary>Loads the databases from files. The files are read as Latin-1.</summary>
    public static SpeciesDatabase Load(string thermoPath, string? transPath = null)
    {
        var thermoBytes = File.ReadAllBytes(thermoPath);
        byte[]? transBytes = transPath is null ? null : File.ReadAllBytes(transPath);
        return Build(
            Encoding.Latin1.GetString(thermoBytes),
            Path.GetFileName(thermoPath),
            Sha256(thermoBytes),
            transBytes is null ? null : Encoding.Latin1.GetString(transBytes),
            transPath is null ? null : Path.GetFileName(transPath),
            transBytes is null ? null : Sha256(transBytes));
    }

    /// <summary>Parses the databases from text. The hashes in <see cref="Provenance"/> are those of the UTF-8 encoding of the text.</summary>
    public static SpeciesDatabase Parse(TextReader thermo, TextReader? trans = null)
    {
        ArgumentNullException.ThrowIfNull(thermo);
        var thermoText = thermo.ReadToEnd();
        var transText = trans?.ReadToEnd();
        return Build(
            thermoText,
            null,
            Sha256(Encoding.UTF8.GetBytes(thermoText)),
            transText,
            null,
            transText is null ? null : Sha256(Encoding.UTF8.GetBytes(transText)));
    }

    private static SpeciesDatabase Build(string thermoText, string? thermoName, string thermoSha, string? transText, string? transName, string? transSha)
    {
        var thermo = ThermoFile.Parse(SplitLines(thermoText), thermoName);
        var transport = transText is null ? null : new TransportDatabase(TransParser.Parse(SplitLines(transText), transName));
        return new SpeciesDatabase(thermo, transport, thermoSha, transSha);
    }

    private static string[] SplitLines(string text) => text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    /// <summary>Every record grouped by exact name, products then reactants, each section in file order.</summary>
    private static Dictionary<string, List<Species>> IndexByName(IReadOnlyList<Species> products, IReadOnlyList<Species> reactants)
    {
        var index = new Dictionary<string, List<Species>>(StringComparer.Ordinal);
        foreach (var species in products.Concat(reactants))
        {
            if (!index.TryGetValue(species.Name, out var list))
            {
                index[species.Name] = list = new List<Species>(1);
            }

            list.Add(species);
        }

        return index;
    }

    /// <summary>
    /// One entry per element symbol that has a monatomic gaseous product species: the first one found in file order,
    /// unless a later one is named exactly for the symbol (case-insensitively), which then wins.
    /// </summary>
    private static Dictionary<string, double> BuildAtomicWeights(IReadOnlyList<Species> products)
    {
        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var species in products)
        {
            if (species.Phase != SpeciesPhase.Gas || species.Formula.Count != 1 || species.Formula[0].Count != 1.0)
            {
                continue;
            }

            var symbol = species.Formula[0].Symbol;
            if (exact.Contains(symbol))
            {
                continue;
            }

            if (string.Equals(species.Name, symbol, StringComparison.OrdinalIgnoreCase))
            {
                weights[symbol] = species.MolarMass;
                exact.Add(symbol);
            }
            else
            {
                weights.TryAdd(symbol, species.MolarMass);
            }
        }

        return weights;
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
