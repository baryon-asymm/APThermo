# API.md — Problems

Namespace `AerospacePropellantThermodynamics.Problems`. The node exposes the
propellant and problem definitions, the result records, and the solver entry points
of the library. Everything not listed here is internal and may change.

## Propellants ⏳

```csharp
namespace AerospacePropellantThermodynamics.Problems;

public enum ReactantRole { Oxidizer, Fuel, Named }      // Named: no oxidizer/fuel split (mass fractions only)

public sealed record Reactant
{
    public static Reactant FromDatabase(string name, ReactantRole role, double amount, double temperature,
                                        AmountKind amountKind = AmountKind.MassFraction);
    public static Reactant Custom(string name, IReadOnlyList<ElementCount> formula, double enthalpy,
                                  double temperature, ReactantRole role, double amount,
                                  double? molarMass = null, AmountKind amountKind = AmountKind.MassFraction);
    public string Name { get; }
    public ReactantRole Role { get; }
    public double Amount { get; }                       // within its role group; normalized by the builder
    public AmountKind AmountKind { get; }
    public double Temperature { get; }                  // K
}

public enum AmountKind { MassFraction, Moles }

public sealed record Propellant
{
    public static PropellantBuilder From(SpeciesDatabase database);
    public IReadOnlyList<Reactant> Reactants { get; }
    public MixtureSpecification Mixture { get; }
    public IReadOnlyList<string> Elements { get; }       // order used in results
}

public abstract record MixtureSpecification
{
    public sealed record OxidizerToFuel(double Ratio) : MixtureSpecification;
    public sealed record MassFractions : MixtureSpecification;   // the reactants' amounts are total mass fractions
}

public sealed class PropellantBuilder
{
    public PropellantBuilder Oxidizer(string name, double temperature, double amount = 1.0);
    public PropellantBuilder Fuel(string name, double temperature, double amount = 1.0);
    public PropellantBuilder Named(string name, double temperature, double massFraction);
    public PropellantBuilder Custom(Reactant reactant);
    public PropellantBuilder OxidizerToFuelRatio(double ratio);
    public PropellantBuilder Omit(params string[] species);
    public PropellantBuilder Only(params string[] species);
    public Propellant Build();
}
```

## Elemental mixtures and state records ⏳

A mixture may be given without reactants, by its element abundances and enthalpy per
kilogram: the form another simulation hands over, and the form the batch path is
built for.

```csharp
public sealed record ElementalMixture
{
    public static ElementalMixture Create(IReadOnlyDictionary<string, double> elementMoles,   // mol per kg
                                          double enthalpy);                                    // J per kg
    public IReadOnlyDictionary<string, double> ElementMoles { get; }   // symbols normalized to the database spelling ("Al" → "AL")
    public double Enthalpy { get; }
    public IReadOnlyList<string> Elements { get; }                    // order used in results
}

public sealed record StateRecord(                     // the exchange record: one state of a mixture
    double Pressure,                                  // Pa
    IReadOnlyDictionary<string, double> Composition,  // element moles, mol per kg
    double? Enthalpy = null,                          // J/kg  → hp problem
    double? Temperature = null,                       // K     → tp problem
    double? Entropy = null);                          // J/(kg·K) → sp problem; exactly one of the three is set

public sealed record StateBatchOptions(bool Transport = false, IReadOnlyList<string>? Omit = null, IReadOnlyList<string>? Only = null);
```

An `ElementalMixture` is accepted everywhere a `Propellant` is (rocket and
equilibrium problems, sweeps over chamber pressure and area ratio). A list of
`StateRecord`s is one batch: every record may carry its own composition, pressure and
target; the candidate species are chosen from the union of the elements of the batch;
an element absent from a record (or zero) makes the species containing it inactive
for that record. Element moles are converted once, here, to the kmol per kg the
numerical nodes use.

## Problems and results ⏳

```csharp
public sealed record RocketProblem
{
    public double ChamberPressure { get; init; }        // Pa
    public FlowModel Flow { get; init; } = FlowModel.ShiftingEquilibrium;
    public IReadOnlyList<double> AreaRatios { get; init; } = [];
    public IReadOnlyList<double> PressureRatios { get; init; } = [];   // p_c / p_e
    public bool Transport { get; init; } = false;
}

public sealed record EquilibriumProblem
{
    public ProblemKind Kind { get; init; }
    public double Pressure { get; init; }               // Pa
    public double Temperature { get; init; }            // K (tp) or estimate
    public double Enthalpy { get; init; }               // J/kg (hp); default: the propellant's h_0
    public double Entropy { get; init; }                // J/(kg·K) (sp)
    public bool Transport { get; init; } = false;
}

public sealed record Station(
    string Name,                                        // "chamber", "throat", "exit[k]"
    MixtureState State,
    IReadOnlyDictionary<string, double> MoleFractions,  // gaseous species, no threshold
    IReadOnlyDictionary<string, double> CondensedMassFractions,
    TransportFigures? Transport,
    CaseStatus Status);

public sealed record RocketResult(
    Propellant Propellant, RocketProblem Problem,
    double OxidizerToFuelRatio, double ReactantEnthalpy, IReadOnlyDictionary<string, double> ElementMoles,
    IReadOnlyList<Station> Stations,                    // chamber, throat, exits in order
    IReadOnlyList<PerformanceFigures> Performance,      // one per exit
    CaseStatus Status,
    AcceleratorInfo Accelerator);

public sealed record EquilibriumResult(
    Propellant? Propellant, ElementalMixture Mixture,    // Propellant is null for elemental input; Mixture is always filled
    EquilibriumProblem Problem, Station State, CaseStatus Status, AcceleratorInfo Accelerator);
```

## Solving ⏳

```csharp
public sealed class Solver : IDisposable
{
    public static Solver Create(SpeciesDatabase database, EngineOptions? options = null);
    public AcceleratorInfo Accelerator { get; }
    public RocketResult Solve(Propellant propellant, RocketProblem problem);
    public IReadOnlyList<RocketResult> Solve(Propellant propellant, IReadOnlyList<RocketProblem> problems);
    public IReadOnlyList<RocketResult> Solve(RocketSweep sweep);
    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem);
    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems);

    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem);
    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems);
    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem);
    public IReadOnlyList<EquilibriumResult> SolveStates(IReadOnlyList<StateRecord> states, StateBatchOptions options);
}

public sealed record RocketSweep(                       // a batch: one reactant set, varying amounts and conditions
    Propellant Propellant,
    IReadOnlyList<double> OxidizerToFuelRatios,
    IReadOnlyList<double> ChamberPressures,
    IReadOnlyList<double> AreaRatios,
    FlowModel Flow,
    bool Transport);
```

A `RocketSweep` expands to the Cartesian product of its lists, all in one batch on
the accelerator; the results are returned in the order ratio-major, then pressure,
then area ratio, one result per (ratio, pressure) with all area ratios as exits.

## Errors

| Situation | Behaviour |
|---|---|
| unknown reactant name | `KeyNotFoundException` naming it, from the builder |
| an element symbol without a database record, a record with none or more than one of enthalpy/temperature/entropy, a negative abundance | `ArgumentException` naming the element or the record index, before any kernel runs |
| reactant temperature outside its record's range; zero-mass group; custom reactant with an element without atomic weight | `ArgumentException` naming the reactant, from `Build` or `Solve`, before any kernel runs |
| accelerator unavailable or ILGPU mismatch | the `Execution` exceptions, unchanged |
| per-case numerical failure | `Status` on the result and on the station; no exception |

## Side effects

None beyond those of `Execution`.

## Out of scope

- Reading files, JSON, command-line options: `Cli`.
- Equivalence ratio, percent fuel: not in version 1.
