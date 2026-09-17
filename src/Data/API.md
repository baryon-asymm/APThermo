# API.md — Data

Namespace `APThermo.Data`. The node exposes the NASA
databases as an immutable object model addressed by species name: everything a
database answers, the index and the atomic weights included, is built by `Load` or
`Parse`, so a loaded instance may be shared between threads without a lock
(2026-09-14: the atomic weights used to be built on first use, the review's F-TD-10).
Everything not listed here is internal and may change.

⚠ 2026-09-15 (distribution phase): the API review of that day
(its finding M1, fixed in `c85fd77`) found that `DatabaseProvenance`, `Species`,
`TemperatureInterval`, `TransportEntry` and `TransportFit` each carried a public
positional constructor no other assembly called: a consumer only ever reads one from
`SpeciesDatabase`. Below, each is now a nominal record with get-only properties and an
internal constructor (root `BOOT.md`, Delivery: Tree contracts, "records the library
creates for consumers ... have internal constructors"), so a field added in 0.x
breaks no consumer. `ElementCount` keeps its public constructor: `Problems`' custom
reactants and the command line build one directly.

## Database ✅

```csharp
namespace APThermo.Data;

public sealed class SpeciesDatabase
{
    public static SpeciesDatabase Load(string thermoPath, string? transPath = null);
    public static SpeciesDatabase LoadBundled();                    // the NASA files embedded in this assembly; same Provenance shape as Load
    public static string BundledNotice();                           // the text of data/NOTICE, embedded in this assembly
    public static SpeciesDatabase Parse(TextReader thermo, TextReader? trans = null);

    public IReadOnlyList<Species> Products { get; }        // PRODUCTS section, file order
    public IReadOnlyList<Species> Reactants { get; }       // REACTANTS section, file order
    public Species this[string name] { get; }              // exact name; the first record of the name, products searched first
    public bool TryGet(string name, out Species species);  // the same first record
    public double AtomicWeight(string element);            // kg/kmol, from the monatomic gaseous species
    public TransportDatabase? Transport { get; }           // null when no trans file was given
    public DatabaseProvenance Provenance { get; }
}

public sealed record DatabaseProvenance                  // internal constructor: only SpeciesDatabase.Load/Parse build one
{
    public string HeaderDate { get; }
    public IReadOnlyList<double> DefaultIntervalBounds { get; }
    public string ThermoSha256 { get; }
    public string? TransSha256 { get; }
}

public sealed record Species                              // internal constructor: only this node parses one
{
    public string Name { get; }                            // as in the file, trailing blanks trimmed
    public string Comment { get; }
    public string DateCode { get; }
    public IReadOnlyList<ElementCount> Formula { get; }    // zero-count pairs dropped, file order
    public SpeciesPhase Phase { get; }
    public double MolarMass { get; }                       // kg/kmol
    public double FormationEnthalpy { get; }               // J/mol at 298.15 K; assigned enthalpy when Intervals is empty
    public double AssignedTemperature { get; }             // K; meaningful only when Intervals is empty
    public IReadOnlyList<TemperatureInterval> Intervals { get; } // file order; bounds as written (see the note below)
    public SpeciesSection Section { get; }
    public bool IsInert { get; }                           // name starts with "Inert" (CEA pseudo-element record)
}

public readonly record struct ElementCount(string Symbol, double Count);

public sealed record TemperatureInterval                  // internal constructor: only this node parses one
{
    public double TLow { get; }
    public double THigh { get; }
    public IReadOnlyList<double> Exponents { get; }        // eight values as in the file
    public IReadOnlyList<double> Coefficients { get; }     // a1 … a7
    public double B1 { get; }
    public double B2 { get; }
    public double EnthalpyOffset { get; }                  // H(298.15) − H(0), J/mol
}

public enum SpeciesPhase { Gas, Condensed }
public enum SpeciesSection { Products, Reactants }
```

`Load` hashes the bytes of each file, `Parse` the UTF-8 text it was given; both hashes
are lowercase hexadecimal SHA-256. Names, comments and references are stored exactly
as in the file with the outer blanks trimmed.

⚠ 2026-09-12: the `Intervals` comment stood "file order, ascending temperature".
Eleven condensed records of the committed file carry a first interval written with an
upper bound not above the lower one (`Br2(cr)` 300..265.9); the node stores such
bounds as they are, and the tests node lists the records in its approved anomaly list.
Found when the loader first rejected them.

⚠ 2026-09-15 (distribution phase): `Load` and `Parse` stood as the only two ways to
build a database. A NuGet package and a .NET tool have no `data/` directory beside
them (root `BOOT.md`, Constraints, Data), so this node embeds the committed
`thermo.inp`, `trans.inp` and `NOTICE` under stable manifest names
(`APThermo.Data.Bundled.thermo.inp`, `.trans.inp`, `.NOTICE`) and `LoadBundled` reads
them: the same bytes as `data/`'s, hashed the same way, so `Provenance` carries the
same `ThermoSha256`/`TransSha256` a caller who ran `Load(data/thermo.inp,
data/trans.inp)` would get. `BundledNotice` exposes the attribution text so a
consumer with only the assembly, not the repository, can still read it.

⚠ 2026-09-14: the indexer's comment stood "exact name; products searched first" and
said nothing of names that carry several records. The committed file has such groups
(`Cr(cr)` twice, `Fe(a)`, `Cr2O3(I)`, the format fact in `BOOT.md`), the indexer
returned the first silently, and `Thermo` had built an index of its own over
`Products` to reach the others (the clean-code review's F-TD-06). `Records` below is
this node's answer, and the indexer keeps returning the first.

## Same-name records ✅

```csharp
public sealed class SpeciesDatabase
{
    public IReadOnlyList<Species> Records(string name);    // every record of the name in file order, products first; empty when the name is unknown
}
```

Declared 2026-09-14, implemented the same day with the `## Structure` decomposition below: the indexer and
`TryGet` still return the first record of a name, and the surface snapshot moves in the same commit.

## Transport database ✅

```csharp
public sealed class TransportDatabase
{
    public IReadOnlyList<TransportEntry> Entries { get; }
    public TransportEntry? Find(string species);                 // single-species entry
    public TransportEntry? FindPair(string first, string second); // order-insensitive
}

public sealed record TransportEntry                       // internal constructor: only this node parses one
{
    public string Species { get; }
    public string? Partner { get; }
    public string Reference { get; }
    public IReadOnlyList<TransportFit> Viscosity { get; }
    public IReadOnlyList<TransportFit> Conductivity { get; }
}

public sealed record TransportFit                         // internal constructor: only this node parses one
{
    public double TLow { get; }
    public double THigh { get; }
    public double A { get; }
    public double B { get; }
    public double C { get; }
    public double D { get; }
}
// ln(property) = A ln T + B/T + C/T² + D; viscosity in micropoise, conductivity in μW/(cm·K), as in the file
```

## Errors ✅

```csharp
public sealed class DatabaseFormatException : Exception
{
    public DatabaseFormatException(string? fileName, int lineNumber, string message, Exception? inner = null);
    public string? FileName { get; }     // null when parsed from a TextReader
    public int LineNumber { get; }       // 1-based line of the offending field
}
```

| Situation | Behaviour |
|---|---|
| a path does not exist | `FileNotFoundException` before anything is parsed |
| a malformed line, a truncated record, a count that does not match the lines present, a negative interval count (2026-09-14: it escaped as an `ArgumentOutOfRangeException` without file or line, F-TD-08) | `DatabaseFormatException` with file name and line number; nothing is returned |
| unknown species name in the indexer | `KeyNotFoundException`; `TryGet` returns `false` and `Records` an empty list instead |
| `AtomicWeight` of an element without a monatomic gaseous record | `KeyNotFoundException` |

## Side effects

`Load` and `Parse` read the given files or text. `LoadBundled` and `BundledNotice`
read no file: the bytes are embedded in the assembly. Nothing else.

## Out of scope

- Evaluating Cp°, H°, S° at a temperature: `Thermo`.
- Choosing which species take part in a problem: `Problems`.
- Unit conversion of any kind.
