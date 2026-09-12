# API.md — Data

Namespace `AerospacePropellantThermodynamics.Data`. The node exposes the NASA
databases as an immutable object model addressed by species name. Everything not
listed here is internal and may change.

## Database ⏳

```csharp
namespace AerospacePropellantThermodynamics.Data;

public sealed class SpeciesDatabase
{
    public static SpeciesDatabase Load(string thermoPath, string? transPath = null);
    public static SpeciesDatabase Parse(TextReader thermo, TextReader? trans = null);

    public IReadOnlyList<Species> Products { get; }        // PRODUCTS section, file order
    public IReadOnlyList<Species> Reactants { get; }       // REACTANTS section, file order
    public Species this[string name] { get; }              // exact name; products searched first
    public bool TryGet(string name, out Species species);
    public double AtomicWeight(string element);            // kg/kmol, from the monatomic gaseous species
    public TransportDatabase? Transport { get; }           // null when no trans file was given
    public DatabaseProvenance Provenance { get; }
}

public sealed record DatabaseProvenance(
    string HeaderDate, IReadOnlyList<double> DefaultIntervalBounds,
    string ThermoSha256, string? TransSha256);

public sealed record Species(
    string Name,                                  // as in the file, trailing blanks trimmed
    string Comment,
    string DateCode,
    IReadOnlyList<ElementCount> Formula,          // zero-count pairs dropped, file order
    SpeciesPhase Phase,
    double MolarMass,                             // kg/kmol
    double FormationEnthalpy,                     // J/mol at 298.15 K; assigned enthalpy when Intervals is empty
    double AssignedTemperature,                   // K; meaningful only when Intervals is empty
    IReadOnlyList<TemperatureInterval> Intervals, // file order, ascending temperature
    SpeciesSection Section,
    bool IsInert);                                // CEA "inert" pseudo-element record

public readonly record struct ElementCount(string Symbol, double Count);

public sealed record TemperatureInterval(
    double TLow, double THigh,
    IReadOnlyList<double> Exponents,              // eight values as in the file
    IReadOnlyList<double> Coefficients,           // a1 … a7
    double B1, double B2,
    double EnthalpyOffset);                       // H(298.15) − H(0), J/mol

public enum SpeciesPhase { Gas, Condensed }
public enum SpeciesSection { Products, Reactants }
```

## Transport database ⏳

```csharp
public sealed class TransportDatabase
{
    public IReadOnlyList<TransportEntry> Entries { get; }
    public TransportEntry? Find(string species);                 // single-species entry
    public TransportEntry? FindPair(string first, string second); // order-insensitive
}

public sealed record TransportEntry(
    string Species, string? Partner, string Reference,
    IReadOnlyList<TransportFit> Viscosity, IReadOnlyList<TransportFit> Conductivity);

public sealed record TransportFit(double TLow, double THigh, double A, double B, double C, double D);
// ln(property) = A ln T + B/T + C/T² + D; viscosity in micropoise, conductivity in μW/(cm·K), as in the file
```

## Errors

| Situation | Behaviour |
|---|---|
| a path does not exist | `FileNotFoundException` before anything is parsed |
| a malformed line, a truncated record, a count that does not match the lines present | `DatabaseFormatException` with file name and line number; nothing is returned |
| unknown species name in the indexer | `KeyNotFoundException`; `TryGet` returns `false` instead |
| `AtomicWeight` of an element without a monatomic gaseous record | `KeyNotFoundException` |

## Side effects

Reads the given files. Nothing else.

## Out of scope

- Evaluating Cp°, H°, S° at a temperature: `Thermo`.
- Choosing which species take part in a problem: `Problems`.
- Unit conversion of any kind.
