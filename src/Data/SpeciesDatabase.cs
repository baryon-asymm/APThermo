using System.Security.Cryptography;
using System.Text;

namespace AerospacePropellantThermodynamics.Data;

/// <summary>The NASA thermodynamic database (and optionally the transport database) as an immutable object model.</summary>
public sealed class SpeciesDatabase
{
    private readonly Dictionary<string, Species> _products;
    private readonly Dictionary<string, Species> _reactants;
    private readonly Dictionary<string, double> _atomicWeights;

    private SpeciesDatabase(ThermoParser.Result thermo, TransportDatabase? transport, string thermoSha256, string? transSha256)
    {
        Products = thermo.Products;
        Reactants = thermo.Reactants;
        Transport = transport;
        Provenance = new DatabaseProvenance(thermo.HeaderDate, thermo.DefaultIntervalBounds, thermoSha256, transSha256);
        _products = Index(thermo.Products);
        _reactants = Index(thermo.Reactants);
        _atomicWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
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
        if (_products.TryGetValue(name, out var product))
        {
            species = product;
            return true;
        }

        if (_reactants.TryGetValue(name, out var reactant))
        {
            species = reactant;
            return true;
        }

        species = null!;
        return false;
    }

    /// <summary>
    /// The atomic weight of an element in kg/kmol: the molar mass of the monatomic gaseous product species with that symbol.
    /// Symbols are matched ignoring case, so both "AL" (the formula spelling) and "Al" work.
    /// </summary>
    public double AtomicWeight(string element)
    {
        if (_atomicWeights.TryGetValue(element, out var cached))
        {
            return cached;
        }

        Species? best = null;
        foreach (var species in Products)
        {
            if (species.Phase != SpeciesPhase.Gas || species.Formula.Count != 1)
            {
                continue;
            }

            var only = species.Formula[0];
            if (only.Count != 1.0 || !string.Equals(only.Symbol, element, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(species.Name, element, StringComparison.OrdinalIgnoreCase))
            {
                best = species;
                break;
            }

            best ??= species;
        }

        if (best is null)
        {
            throw new KeyNotFoundException($"no monatomic gaseous species for element '{element}'; its atomic weight is unknown");
        }

        _atomicWeights[element] = best.MolarMass;
        return best.MolarMass;
    }

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
        var thermo = ThermoParser.Parse(SplitLines(thermoText), thermoName);
        var transport = transText is null ? null : new TransportDatabase(TransParser.Parse(SplitLines(transText), transName));
        return new SpeciesDatabase(thermo, transport, thermoSha, transSha);
    }

    private static string[] SplitLines(string text) => text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    private static Dictionary<string, Species> Index(IReadOnlyList<Species> list)
    {
        var index = new Dictionary<string, Species>(list.Count, StringComparer.Ordinal);
        foreach (var species in list)
        {
            index.TryAdd(species.Name, species);
        }

        return index;
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
