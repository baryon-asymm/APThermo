# API.md — Thermo

Namespace `AerospacePropellantThermodynamics.Thermo`. The node exposes compact species
tables, the kernel-compatible species functions, and the vocabulary shared by the
numerical nodes. Everything not listed here is internal and may change.

## Constants and shared vocabulary ⏳

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
    public double Enthalpy, Entropy, GibbsEnergy; // J/kg, J/(kg·K), J/kg
    public double MolarMass;                      // kg/kmol, CEA's M = 1/n
    public double CpFrozen, CpEquilibrium;        // J/(kg·K)
    public double DlnVdlnT, DlnVdlnP;             // equilibrium derivatives, dimensionless
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

## Species table ⏳

```csharp
public sealed class SpeciesTable                          // host side, immutable
{
    public static SpeciesTable Build(SpeciesDatabase database,
                                     IReadOnlyList<string> elements,
                                     IReadOnlyList<string> species);   // gaseous first, then condensed
    public IReadOnlyList<string> Elements { get; }
    public IReadOnlyList<string> Species { get; }
    public int SpeciesCount { get; }
    public int GasCount { get; }
    public int CondensedCount { get; }
    public SpeciesTableArrays Arrays { get; }                // flat host arrays, ready to upload
    public SpeciesTableView HostView { get; }                // a view over the host arrays
}

public sealed class SpeciesTableArrays
{
    public double[] MolarMass;          // [species], kg/kmol
    public double[] FormationEnthalpy;  // [species], J/mol
    public double[] Stoichiometry;      // [element * SpeciesCount + species], atoms per formula unit
    public int[] IntervalStart;         // [species]
    public int[] IntervalCount;         // [species]
    public double[] IntervalBounds;     // [interval * 2 + (0: TLow, 1: THigh)]
    public double[] Exponents;          // [interval * 8 + k]
    public double[] Coefficients;       // [interval * 9 + k]: a1 … a7, b1, b2
}

public readonly struct SpeciesTableView                    // blittable; the same layout as the arrays
{
    public readonly int SpeciesCount, GasCount, ElementCount;
    public readonly ArrayView<double> MolarMass, FormationEnthalpy, Stoichiometry;
    public readonly ArrayView<int> IntervalStart, IntervalCount;
    public readonly ArrayView<double> IntervalBounds, Exponents, Coefficients;
}

public static class TableLimits
{
    public const int MaxElements = 20;
    public const int MaxSpecies = 2048;
    public const int MaxIntervalsPerSpecies = 5;
}
```

## Species functions ⏳

```csharp
public static class SpeciesFunctions                       // kernel-compatible
{
    public static double CpOverR(in SpeciesTableView table, int species, double temperature);
    public static double HOverRT(in SpeciesTableView table, int species, double temperature);
    public static double SOverR(in SpeciesTableView table, int species, double temperature);
    public static double GOverRT(in SpeciesTableView table, int species, double temperature); // H/RT − S/R
    public static int IntervalOf(in SpeciesTableView table, int species, double temperature);  // nearest interval
    public static bool IsInRange(in SpeciesTableView table, int species, double temperature);
}
```

`temperature` is in K and must be positive; the functions are dimensionless. Species
indices are those of the table. The functions are safe to call from any thread and
from kernels.

## Errors

| Situation | Behaviour |
|---|---|
| unknown species or element name in `Build` | `KeyNotFoundException` naming it |
| a species with an element outside `elements` | `ArgumentException` naming the species and the element |
| more species, elements or intervals than `TableLimits` | `ArgumentException` |
| a species function with an index outside the table | undefined in kernels; callers guarantee the range |

## Side effects

None.

## Out of scope

- Mixture properties (enthalpy of a composition, entropy with mixing terms): `Equilibrium`.
- Which species and elements to put into a table: `Problems`.
- Uploading tables to an accelerator: `Execution`.
