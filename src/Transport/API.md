# API.md — Transport

Namespace `APThermo.Transport`. The node exposes a compact
transport table for the species of a species table and a kernel-compatible evaluation
of the mixture transport properties at one station. Everything not listed here, in a
package-surface section (one whose heading carries no `(tree contract)` mark), is
internal and may change without notice (root `BOOT.md`, Delivery: Public surface).
The tree-contract sections below list the internal types `Execution` and `Problems`
use (root `BOOT.md`, Delivery: Tree contracts); the assembly grants
`InternalsVisibleTo` to those nodes, to `Execution.Tests`, `Problems.Tests` and
`Benchmarks`, and to `ILGPURuntime` for `TransportTableView`, a kernel parameter type
(`APThermo.Transport.csproj`).

⚠ 2026-09-15 (distribution phase): the review of that day
(fixed in `c11e02b`) found no consumer scenario for `TransportTable`,
`TransportTableArrays`, `TransportTableView`, `TransportTableBuffers`,
`TransportLayout`, `TransportScratch` or `TransportSolver`: every use is `Execution`
composing the kernel, `Problems` building a table, or this node's own tests. They
moved from the package surface into the tree contract below; only `TransportFigures`
stays public, because a consumer reads it from `Station.Transport` of `Problems`.

## Transport table (tree contract) ✅

```csharp
internal sealed class TransportTable                       // host side, immutable
{
    public const double ViscosityFactorToSi = 1e-7;      // 1 μP in Pa·s, folded into the constant term of every viscosity fit
    public const double ConductivityFactorToSi = 1e-4;   // 1 μW/(cm·K) in W/(m·K), likewise for conductivity fits
    public const int FitStride = 6;                      // doubles per fit: TLow, THigh, A, B, C, D
    public static TransportTable Build(TransportDatabase database, SpeciesTable species);
    public SpeciesTable Species { get; }
    public IReadOnlyList<string> SpeciesWithData { get; }       // gaseous species with a viscosity fit, table order
    public IReadOnlyList<string> SpeciesWithoutData { get; }    // gaseous species without an entry, table order; estimated by the solver
    public IReadOnlyList<(string First, string Second)> Pairs { get; }  // pairs of gaseous species with interaction data, database order
    public TransportTableArrays Arrays { get; }
    public int SpeciesCount { get; }
}

internal sealed class TransportTableArrays                 // do not modify after the build
{
    public int[] ViscosityStart { get; }        // [species]
    public int[] ViscosityCount { get; }        // [species], 0 = no data (and for condensed species)
    public int[] ConductivityStart { get; }     // [species]
    public int[] ConductivityCount { get; }     // [species], 0 = no conductivity fit
    public double[] Fits { get; }               // [fit * 6]: TLow, THigh, A, B, C, D with the SI factor in D
    public int[] PairIndex { get; }             // [species * SpeciesCount + species], −1 = no pair data
    public int[] PairStart { get; }             // [pair] (one padding entry when there is no pair)
    public int[] PairCount { get; }             // [pair]
    public int PairTotal { get; }
    public int FitTotal { get; }
}

internal readonly struct TransportTableView                // blittable; the same layout over accelerator memory
{
    public readonly int SpeciesCount, PairTotal;
    public readonly ArrayView<int> ViscosityStart, ViscosityCount, ConductivityStart, ConductivityCount;
    public readonly ArrayView<double> Fits;
    public readonly ArrayView<int> PairIndex, PairStart, PairCount;
    public TransportTableView(int speciesCount, int pairTotal,
                              ArrayView<int> viscosityStart, ArrayView<int> viscosityCount,
                              ArrayView<int> conductivityStart, ArrayView<int> conductivityCount,
                              ArrayView<double> fits, ArrayView<int> pairIndex, ArrayView<int> pairStart, ArrayView<int> pairCount);
}

internal sealed class TransportTableBuffers : IDisposable  // the table uploaded to one accelerator; owns the buffers
{
    public static TransportTableBuffers Upload(Accelerator accelerator, TransportTable table);
    public TransportTable Table { get; }
    public TransportTableView View { get; }              // pass to kernels; on the CPU accelerator, usable from host code too
    public void Dispose();
}
```

A species is looked up by its exact name; a pair entry is taken when both of its
species are gaseous members of the table. A species with an entry that has viscosity
fits but no conductivity fits keeps its viscosity fits and a zero conductivity count.

⚠ 2026-09-12: the sketch had `TransportTableView HostView` on the table. As for the
species table of `Thermo`, ILGPU 1.5.3 offers no view over a managed array outside a
kernel; `TransportTableBuffers.Upload` replaces it. The sketch also had one start and
count per species for the pair runs; the pairs got their own start and count arrays
addressed through `PairIndex`.

## Evaluation ✅

```csharp
public struct TransportFigures : IEquatable<TransportFigures>   // one station; SI
{
    public double Viscosity { get; set; }                // Pa·s
    public double FrozenConductivity { get; set; }       // W/(m·K)
    public double ReactingConductivity { get; set; }     // W/(m·K), frozen plus reaction term
    public double FrozenPrandtl { get; set; }            // Cp_fr η / λ_fr over the transport set
    public double ReactingPrandtl { get; set; }          // Cp_eq η / λ_eq over the transport set
    public double FrozenHeatCapacity { get; set; }       // J/(kg·K) of the set's gas: the reference's cp_fr with transport on
    public double EquilibriumHeatCapacity { get; set; }  // J/(kg·K), frozen plus reaction heat capacity of the set
    public double EstimatedMoleFraction { get; set; }    // of the set, carried by species without data
    public int SpeciesCount { get; set; }                // NM
    public int ReactionCount { get; set; }               // NR, after the trace eliminations
    public int EstimatedSpeciesCount { get; set; }
    public int TraceEliminations { get; set; }           // species of the set below TraceFraction removed from the reaction set
    public int Capped { get; set; }                      // 1 when a species was refused because the set was full

    public readonly bool Equals(TransportFigures other);       // every property equal by Equals of its type (NaN equals NaN)
    public override readonly bool Equals(object? obj);
    public override readonly int GetHashCode();
    public static bool operator ==(TransportFigures left, TransportFigures right);
    public static bool operator !=(TransportFigures left, TransportFigures right);
}
```

⚠ 2026-09-24: until 0.1.0 the members were public fields without equality; the root's
Diagnostics constraint (CA1051, CA1815) made them auto-properties with value equality.
The consequences are the same as for `MixtureState` (`src/Thermo/API.md`):
source-compatible for readers, a recompile for binaries built against 0.1.0, and
unchanged layout and bits.

## Solver (tree contract) ✅

```csharp
internal static class TransportLayout
{
    public const int MaxSpecies = 40;                                          // the reference's limit on the set
    public static int DoublesPerCase(int elementCount);                        // 4 · MaxSpecies² + elements · MaxSpecies + 8 · MaxSpecies
    public static int IntsPerCase(int speciesCount, int elementCount);         // species + 4 · MaxSpecies + 4 · elements
}

internal readonly struct TransportScratch                  // slices of batch-sized buffers, sized by TransportLayout
{
    public readonly ArrayView<double> Eta, Alpha, Matrix, MatrixReacting;      // [MaxSpecies²] each, row-major, stride MaxSpecies
    public readonly ArrayView<double> Basis;                                   // [elements · MaxSpecies]
    public readonly ArrayView<double> Cond, Xs, Cp, H, DeltaH, Rhs, RowScale, Stx;  // [MaxSpecies] each
    public readonly ArrayView<int> Mark;                                       // [species]
    public readonly ArrayView<int> IndexList, CompLocal, CompRow, IsComponent; // [MaxSpecies] each
    public readonly ArrayView<int> Component, Default, RowTaken, RowActive;    // [elements] each
    public TransportScratch(ArrayView<double> eta, ArrayView<double> alpha, ArrayView<double> matrix, ArrayView<double> matrixReacting,
                            ArrayView<double> basis, ArrayView<double> cond, ArrayView<double> xs, ArrayView<double> cp, ArrayView<double> h,
                            ArrayView<double> deltaH, ArrayView<double> rhs, ArrayView<double> rowScale, ArrayView<double> stx,
                            ArrayView<int> mark, ArrayView<int> indexList, ArrayView<int> compLocal, ArrayView<int> compRow,
                            ArrayView<int> isComponent, ArrayView<int> component, ArrayView<int> @default, ArrayView<int> rowTaken,
                            ArrayView<int> rowActive);
    public static TransportScratch Slice(ArrayView<double> doubles, ArrayView<int> ints, int speciesCount, int elementCount);
}

internal static class TransportSolver                      // kernel-compatible
{
    public const int MaxSpecies = TransportLayout.MaxSpecies;
    public const double CoverageFraction = 0.999999999;  // the set is complete when it carries this fraction of the gaseous moles …
    public const double CoverageTolerance = 1e-6;        // … within this relative slack
    public const double CutoffFraction = 1e-11;          // the selection never descends below this fraction of the gaseous moles
    public const double TraceFraction = 1e-10;           // a species of the set below this mole fraction leaves the reaction set
    public const double BasisCleaningThreshold = 1e-5;
    public const double ReactionCoefficientThreshold = 1e-6;
    public const double EliminationThreshold = 1e-5;
    public const double StoichiometryThreshold = 1e-10;
    public const double UnitCountTolerance = 1e-8;
    public const double AStar = 1.1;
    public const double Boltzmann = 1.3806580e-23;       // J/K, the reference's value
    public const double Avogadro = 6.0221367e26;         // 1/kmol, the reference's value
    public const double CollisionDiameter = 1e-10;       // m, of the hard-sphere estimate
    public static CaseStatus Evaluate(in SpeciesTableView species, in TransportTableView transport, double temperature,
                                      ArrayView<double> moles,              // [species], kmol per kg, condensed included (ignored)
                                      in TransportScratch scratch,
                                      ArrayView<TransportFigures> figures); // [1], written on every status
    public static int FitOf(in TransportTableView transport, int start, int count, double temperature);   // global fit index by the reference's rule
    public static double FitValue(in TransportTableView transport, int fit, double temperature);         // exp(A ln T + B/T + C/T² + D), SI
    public static double PureViscosity(in TransportTableView transport, int species, double temperature);     // Pa·s; 0 without data
    public static double PureConductivity(in TransportTableView transport, int species, double temperature);  // W/(m·K); 0 without data
    public static double PairViscosity(in TransportTableView transport, int pair, double temperature);        // Pa·s
}
```

`temperature` is in K; the transport set, the estimates and the reaction terms follow
the Constraints of `BOOT.md`. The evaluation reads the moles of every species (a
condensed species with positive moles marks its elements active) and uses the gaseous
ones of the case, those whose every element is active: a table may hold the species
of elements the case lacks (a batch over a union of elements), and they neither
enter the set nor count in its thresholds (`BOOT.md`, Invariants).

⚠ 2026-09-12: the sketch was `Evaluate(in species, in transport, in MixtureState state,
moles, multipliers, ArrayView<double> scratch, out TransportFigures figures)` with
`ScratchDoubles(int speciesCount)`, and the figures held `ExcludedMoleFraction`. The
state's only input is the temperature, so it is passed alone; the multipliers are not
needed (`BOOT.md`); the scratch is a struct of named slices with ints as well, like
`Equilibrium`'s, because the set selection and the component basis need index arrays;
the figures are written into a view, as every numerical node does, and report the
estimated species and the set's bookkeeping instead of an excluded fraction.

Two slots of the scratch are shared between stages of the evaluation and hold
nothing across a call: `Stx` is the normalised pivot row of the trace elimination
and, later, the per-pair difference vector of the reaction terms; `Mark` carries
"seen by the component search" and "in the set" for every species. A caller slices
the scratch and reads nothing from it. `TransportLayout.DoublesPerCase` takes
only the element count: the double scratch is `4·M² + E·M + 8·M` with `M = MaxSpecies`,
and the set is capped at `MaxSpecies`, so the doubles per case do not grow with the
table.

⚠ 2026-09-24: it took the species count too and ignored it, so that the execution
node would size every scratch the way `Equilibrium`'s `ScratchLayout` does (recorded
2026-09-14, the clean-code review's F-TP-06 and F-TP-07). Under the root's Diagnostics
constraint the unused parameter is IDE0060, and a discard that only silenced the rule
was found at the review of the first Diagnostics branch. The parameter is gone, and the
execution node passes the element count alone.

## Errors

Never throws. `Evaluate` returns `Ok`; `InvalidInput` (non-positive or NaN
temperature, a negative or NaN mole number, a transport table whose species count is
not the species table's, an empty or gas-free table); `NoTransportData` (no gaseous
species with positive moles); `SingularMatrix` (a reaction system could not be solved:
the frozen figures are written and the reacting ones equal them, the reacting
conductivity, the equilibrium heat capacity and the reacting Prandtl number alike).
`figures[0]` is written on every status, zero on `InvalidInput` and `NoTransportData`.

⚠ 2026-09-14: the sentence was the contract, and the code kept only half of it: when
the second of the two reaction systems could not be solved, the reacting conductivity
fell back to the frozen one while the reaction heat capacity of the first system
survived into `EquilibriumHeatCapacity` and `ReactingPrandtl`. Found by the
clean-code review (F-TP-01); the code now obeys the sentence, and the tests node
holds the status to it (the parent's `BOOT.md`, acceptance criteria). `Build` throws
`ArgumentNullException` for a null argument and nothing else: a species without an
entry is not an error.

## Side effects

None. Writes only into `figures` and the scratch.

## Out of scope

- Parsing `trans.inp`: `Data`.
- Deciding at which stations transport is evaluated: `Problems` and `Execution`.
- Computing the composition: `Equilibrium`, `Performance`.
