# API.md — Thermo

Namespace `AerospacePropellantThermodynamics.Thermo`. The node exposes compact species
tables, the kernel-compatible species functions, and the vocabulary shared by the
numerical nodes. Everything not listed here is internal and may change.

## Constants and shared vocabulary ✅

```csharp
namespace AerospacePropellantThermodynamics.Thermo;

public static class PhysicalConstants
{
    public const double R = 8314.51;              // J/(kmol·K), the value NASA CEA uses
}

public struct MixtureState                        // one station of one case; SI units
{
    public double Temperature;                    // K
    public double Pressure;                       // Pa
    public double Density;                        // kg/m³
    public double Enthalpy, InternalEnergy;       // J/kg
    public double Entropy;                        // J/(kg·K)
    public double GibbsEnergy;                    // J/kg
    public double MolarMass;                      // kg/kmol, CEA's M = 1/n (whole mixture per kmol of gas)
    public double MixtureMolarMass;               // kg/kmol, CEA's MW: one kilogram over the moles of all species, condensed included
    public double CpFrozen, CpEquilibrium;        // J/(kg·K)
    public double CvFrozen, CvEquilibrium;        // J/(kg·K)
    public double DlnVdlnT, DlnVdlnP;             // equilibrium derivatives, dimensionless; 1 and −1 when frozen
    public double GammaS;                         // isentropic exponent
    public double SoundSpeed;                     // m/s
    public double Velocity, Mach;                 // zero where not applicable
}

public enum CaseStatus
{
    Ok = 0,
    InvalidInput,
    NotConverged,
    SingularMatrix,
    TemperatureOutOfRange,
    ThroatNotFound,
    AreaRatioInvalid,
    NoTransportData,
}
```

`InternalEnergy`, `MixtureMolarMass`, `CvFrozen` and `CvEquilibrium` were added to the
sketch when the reference fixtures turned out to report them; a numerical node that
does not compute a field leaves it zero and says so in its `API.md`.

⚠ 2026-09-12: the field was `GasMolarMass`, "CEA's MW (gaseous part per kmol of gas)".
Wrong: the reference's MW is 1/Σ n_j over all species with the condensed ones counted
as moles (it equals M for a gas-only mixture), found when the Equilibrium tests compared
the water-condensation example (M = 64.18, MW = 19.29 kg/kmol at 300 K) and the
aluminized propellant. Renamed, with the fixture field and the tolerance entry.

## Species table ✅

```csharp
public sealed class SpeciesTable                          // host side, immutable
{
    public static SpeciesTable Build(SpeciesDatabase database,
                                     IReadOnlyList<string> elements,
                                     IReadOnlyList<string> species);   // gaseous first, then condensed, each in the given order
    public IReadOnlyList<string> Elements { get; }           // as given; the row order of the stoichiometry matrix
    public IReadOnlyList<string> Species { get; }            // table order
    public IReadOnlyList<Species> Records { get; }           // the Data records in table order
    public int SpeciesCount { get; }
    public int GasCount { get; }
    public int CondensedCount { get; }
    public int ElementCount { get; }
    public SpeciesTableArrays Arrays { get; }                // flat host arrays, ready to upload
    public int IndexOf(string species);                      // table index, or −1
}

public sealed class SpeciesTableArrays                       // do not modify after the build
{
    public double[] MolarMass { get; }          // [species], kg/kmol
    public double[] FormationEnthalpy { get; }  // [species], J/mol
    public double[] Stoichiometry { get; }      // [element * SpeciesCount + species], atoms per formula unit
    public int[] IntervalStart { get; }         // [species]
    public int[] IntervalCount { get; }         // [species]
    public double[] IntervalBounds { get; }     // [interval * 2 + (0: TLow, 1: THigh)]
    public double[] Exponents { get; }          // [interval * 8 + k]
    public double[] Coefficients { get; }       // [interval * 9 + k]: a1 … a7, b1, b2
    public int IntervalTotal { get; }
}

public readonly struct SpeciesTableView                    // blittable; the same layout over accelerator memory
{
    public readonly int SpeciesCount, GasCount, ElementCount;
    public readonly ArrayView<double> MolarMass, FormationEnthalpy, Stoichiometry;
    public readonly ArrayView<int> IntervalStart, IntervalCount;
    public readonly ArrayView<double> IntervalBounds, Exponents, Coefficients;
    public SpeciesTableView(int speciesCount, int gasCount, int elementCount,
                            ArrayView<double> molarMass, ArrayView<double> formationEnthalpy, ArrayView<double> stoichiometry,
                            ArrayView<int> intervalStart, ArrayView<int> intervalCount,
                            ArrayView<double> intervalBounds, ArrayView<double> exponents, ArrayView<double> coefficients);
}

public sealed class SpeciesTableBuffers : IDisposable        // the table uploaded to one accelerator; owns the buffers
{
    public static SpeciesTableBuffers Upload(Accelerator accelerator, SpeciesTable table);
    public SpeciesTable Table { get; }
    public SpeciesTableView View { get; }                    // pass to kernels; on the CPU accelerator, usable from host code too
    public void Dispose();
}

public static class TableLimits
{
    public const int MaxElements = 20;
    public const int MaxSpecies = 2048;
    public const int MaxIntervalsPerSpecies = 5;
}
```

Element symbols of `elements` are matched to the records' formula symbols
case-insensitively (`Al` and `AL` name the same row); species names are exact.

⚠ 2026-09-12: the sketch had `SpeciesTable.HostView`, "a view over the host arrays".
ILGPU 1.5.3 offers no view over a managed array outside a kernel; a view needs a
buffer of an accelerator, so `SpeciesTableBuffers.Upload(accelerator, table)` replaces
it, and the host path uses the CPU accelerator. See the note in `BOOT.md`.

## Species functions ✅

```csharp
public static class SpeciesFunctions                       // kernel-compatible
{
    public static double CpOverR(in SpeciesTableView table, int species, double temperature);
    public static double HOverRT(in SpeciesTableView table, int species, double temperature);
    public static double SOverR(in SpeciesTableView table, int species, double temperature);
    public static double GOverRT(in SpeciesTableView table, int species, double temperature); // H/RT − S/R
    public static int IntervalOf(in SpeciesTableView table, int species, double temperature);  // 0-based within the species: the first whose upper bound is not below T, else the last
    public static bool IsInRange(in SpeciesTableView table, int species, double temperature); // first lower bound ≤ T ≤ last upper bound
}
```

`temperature` is in K and must be positive; the functions are dimensionless. Species
indices are those of the table. The functions are safe to call from any thread and
from kernels; the usual exponents −2 … 4 are evaluated by multiplication, any other
through `Math.Pow`.

## Join-and-cut of condensed records ✅

```csharp
public static class SpeciesFunctions
{
    public const double LatentHeatThreshold = 1.0e-3;   // |ΔH°/RT| at a shared bound: at or above it, two adjacent condensed fits are a real transition
}

public sealed class SpeciesTable
{
    public IReadOnlyList<int> IndicesOf(string species);   // the pieces of a database name in table order, or its one entry; empty when the table lacks the name
}
```

`SpeciesTable.Build` concatenates condensed product records that share one name (when
their formulas and molar masses agree and their ranges touch) into one table species,
and splits a condensed species at every internal interval bound where the two fits
differ by `|ΔH°/RT| ≥ LatentHeatThreshold`, each piece named `NAME[TLow-THigh]` in
kelvin (`ALN(L)[1800-2700]`, `ALN(L)[2700-6000]`). `Species`, `SpeciesCount`,
`CondensedCount` and `IndexOf` speak of table species, which need not be one-to-one
with the given names; `IndicesOf` maps a database name to its pieces, adjacent and
ascending in the record's place; `Records` maps each table species to the record that
provided its first interval; records of one name that cannot concatenate are refused
with an `ArgumentException` naming them. The rule and the threshold's derivation are
in `BOOT.md`.

## Errors

| Situation | Behaviour |
|---|---|
| unknown species name in `Build` | `KeyNotFoundException` naming it (from the database indexer) |
| a species with an element outside `elements` | `ArgumentException` naming the species and the element |
| a species listed twice, an element listed twice, an empty list, or a reactant-only record without polynomial intervals (`O2(L)`) | `ArgumentException` naming it |
| more species, elements or intervals than `TableLimits` | `ArgumentException`, checked before any lookup |
| a species function with an index outside the table | undefined in kernels; callers guarantee the range |

## Side effects

None.

## Out of scope

- Mixture properties (enthalpy of a composition, entropy with mixing terms): `Equilibrium`.
- Which species and elements to put into a table: `Problems`.
- Uploading tables to an accelerator: `Execution`.
