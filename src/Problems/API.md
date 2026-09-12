# API.md — Problems

Namespace `AerospacePropellantThermodynamics.Problems`. The node exposes the
propellant and problem definitions, the result records, and the solver entry points
of the library. Everything not listed here is internal and may change. Types of the
neighbours appear in the signatures: `SpeciesDatabase` and `ElementCount` (`Data`),
`MixtureState` and `CaseStatus` (`Thermo`), `ProblemKind` (`Equilibrium`),
`FlowModel` and `PerformanceFigures` (`Performance`), `TransportFigures`
(`Transport`), `EngineOptions` and `AcceleratorInfo` (`Execution`).

## Propellants ✅

```csharp
namespace AerospacePropellantThermodynamics.Problems;

public enum ReactantRole { Oxidizer, Fuel, Named }      // Named: a total mass fraction, outside any oxidizer/fuel split

public enum AmountKind { MassFraction, Moles }

public sealed record Reactant
{
    public const double DefaultTemperature = 298.15;    // K: a record with polynomial intervals, no temperature given
    public static Reactant FromDatabase(string name, ReactantRole role, double amount, double? temperature = null,
                                        AmountKind amountKind = AmountKind.MassFraction);
    public static Reactant Custom(string name, IReadOnlyList<ElementCount> formula, double enthalpy,   // J/mol at temperature
                                  double temperature, ReactantRole role, double amount,
                                  double? molarMass = null, AmountKind amountKind = AmountKind.MassFraction);
    public string Name { get; }
    public ReactantRole Role { get; }
    public double Amount { get; }                       // within its role group; normalized by the builder
    public AmountKind AmountKind { get; }
    public double? Temperature { get; }                 // K; null = the record's default
    public bool IsCustom { get; }
    public IReadOnlyList<ElementCount>? Formula { get; }   // custom reactants only
    public double? Enthalpy { get; }                    // custom reactants only, J/mol
    public double? MolarMass { get; }                   // custom reactants only, kg/kmol; null = from the formula and the atomic weights
}

public abstract record MixtureSpecification
{
    public sealed record OxidizerToFuel(double Ratio) : MixtureSpecification;
    public sealed record MassFractions : MixtureSpecification;   // the reactants' amounts are total mass fractions
}

public sealed record Propellant
{
    public static PropellantBuilder From(SpeciesDatabase database);
    public IReadOnlyList<Reactant> Reactants { get; }
    public MixtureSpecification Mixture { get; }
    public IReadOnlyList<string> Elements { get; }       // database spelling; oxidizers, fuels, named reactants, by first appearance
    public IReadOnlyList<string> Omit { get; }
    public IReadOnlyList<string>? Only { get; }
    public double? OxidizerToFuelRatio { get; }          // the mixture rule's ratio, or null for total mass fractions
}

public sealed class PropellantBuilder
{
    public const double TemperatureMargin = 10.0;       // K beyond the record's range that is still accepted
    public PropellantBuilder Oxidizer(string name, double? temperature = null, double amount = 1.0, AmountKind amountKind = AmountKind.MassFraction);
    public PropellantBuilder Fuel(string name, double? temperature = null, double amount = 1.0, AmountKind amountKind = AmountKind.MassFraction);
    public PropellantBuilder Named(string name, double massFraction, double? temperature = null);
    public PropellantBuilder Custom(Reactant reactant);
    public PropellantBuilder Add(Reactant reactant);
    public PropellantBuilder OxidizerToFuelRatio(double ratio);
    public PropellantBuilder Omit(params string[] species);
    public PropellantBuilder Only(params string[] species);
    public Propellant Build();
}
```

`Build` resolves every name against the database, applies the default temperatures
(298.15 K for a record with intervals, the assigned temperature for one without),
checks each temperature against its record's range widened by `TemperatureMargin`,
converts mole amounts to masses with the record's molar mass, derives a custom
reactant's molar mass from its formula and the database's atomic weights, and
validates the mixture rule: a ratio needs at least one oxidizer and one fuel and no
named reactant; without a ratio, oxidizers and fuels may not be mixed; every group
has positive mass. `Only` is checked against the database and the elements at `Build`.

⚠ 2026-09-12: the sketch had a mandatory `double temperature` on every reactant, a
`Named(name, temperature, massFraction)` order, and no `Omit`, `Only` or ratio on
`Propellant`. Temperatures are optional because the reference assumes 298.15 K or the
record's own temperature, and the fixtures were generated that way; the lists and the
ratio are on `Propellant` because the mixture and the species selection travel
together into every solve. `Add`, `DefaultTemperature` and `TemperatureMargin` are new.

## Elemental mixtures and state records ✅

A mixture may be given without reactants, by its element abundances and enthalpy per
kilogram: the form another simulation hands over, and the form the batch path is
built for.

```csharp
public sealed record ElementalMixture
{
    public static ElementalMixture Create(IReadOnlyDictionary<string, double> elementMoles,   // mol per kg
                                          double? enthalpy = null,                             // J per kg; null: assigned-temperature problems only
                                          IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null);
    public IReadOnlyDictionary<string, double> ElementMoles { get; }   // symbols in the database spelling ("Al" → "AL")
    public double? Enthalpy { get; }
    public IReadOnlyList<string> Elements { get; }                    // order used in results
    public IReadOnlyList<string> Omit { get; }
    public IReadOnlyList<string>? Only { get; }
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
equilibrium problems). A list of `StateRecord`s is one batch: every record may carry
its own composition, pressure and target; the candidate species are chosen from the
union of the elements of the batch (in order of first appearance) under the options'
lists; an element absent from a record (or zero) makes the species containing it
inactive for that record. Element moles are converted once, here, to the kmol per kg
the numerical nodes use.

⚠ 2026-09-12: the sketch's `Create(elementMoles, enthalpy)` had a mandatory enthalpy
and no species lists. A mixture solved only at assigned temperatures has no enthalpy
to give, and the lists belong to the mixture so that a state batch and a propellant
select species the same way.

## Problems and results ✅

```csharp
public sealed record RocketProblem
{
    public double ChamberPressure { get; init; }        // Pa
    public FlowModel Flow { get; init; } = FlowModel.ShiftingEquilibrium;
    public IReadOnlyList<double> PressureRatios { get; init; } = [];   // p_c / p_e, reported first
    public IReadOnlyList<double> AreaRatios { get; init; } = [];       // supersonic A / A_t, reported after
    public double TemperatureEstimate { get; init; }    // K for the chamber solve; 0 = the equilibrium node's default
    public bool Transport { get; init; }
    public int ExitCount { get; }                       // PressureRatios.Count + AreaRatios.Count
}

public sealed record EquilibriumProblem
{
    public ProblemKind Kind { get; init; } = ProblemKind.AssignedEnthalpyPressure;
    public double Pressure { get; init; }               // Pa
    public double Temperature { get; init; }            // K: assigned for tp, the estimate otherwise (0 = default)
    public double? Enthalpy { get; init; }              // J/kg for hp; null = the mixture's enthalpy
    public double Entropy { get; init; }                // J/(kg·K) for sp
    public bool Transport { get; init; }
}

public sealed record RocketSweep(                       // a batch: one reactant set, varying ratio and pressure
    Propellant Propellant,
    IReadOnlyList<double> OxidizerToFuelRatios,
    IReadOnlyList<double> ChamberPressures,
    IReadOnlyList<double> AreaRatios,
    FlowModel Flow = FlowModel.ShiftingEquilibrium,
    bool Transport = false);

public sealed record Station(
    string Name,                                        // "chamber", "throat", "exit1", "exit2", …; "state" for an equilibrium result
    MixtureState State,                                 // zero where the status is not Ok
    PerformanceFigures? Performance,                    // rocket stations only
    IReadOnlyDictionary<string, double> MoleFractions,  // every species of the table, n_j over the moles of all species
    IReadOnlyDictionary<string, double> CondensedMassFractions,   // every condensed species, n_j M_j
    TransportFigures? Transport,                        // null when not requested or not Ok
    CaseStatus? TransportStatus,                        // null when transport was not requested
    CaseStatus Status);

public sealed record RocketResult(
    Propellant? Propellant,                             // null for an elemental mixture
    ElementalMixture Mixture,                           // the element moles and enthalpy the case started from
    RocketProblem Problem,
    double? OxidizerToFuelRatio,                        // the ratio of the mixture rule, or null
    IReadOnlyList<string> Species,                      // table order: gases, then condensed species
    IReadOnlyList<Station> Stations,                    // chamber, throat, exits in order
    CaseStatus Status,
    AcceleratorInfo Accelerator);

public sealed record EquilibriumResult(
    Propellant? Propellant,
    ElementalMixture Mixture,
    EquilibriumProblem Problem,
    IReadOnlyList<string> Species,
    Station State,
    CaseStatus Status,
    AcceleratorInfo Accelerator);
```

A `RocketSweep` expands to every (ratio, chamber pressure) pair, all in one batch on
the accelerator; the results come back ratio-major, then by pressure, one result per
pair with all area ratios as exits.

⚠ 2026-09-12: the sketch's `RocketResult` carried `ReactantEnthalpy`, `ElementMoles`
and a `Performance` list per exit next to the stations, its `Propellant` was never
null, and `Station.MoleFractions` was over the gaseous phase. The mixture the case
started from is one record for both front doors (`Mixture`, always filled;
`Propellant` null for an elemental mixture, as the sketch already had for equilibrium
results); the performance figures sit on the station they belong to, as the
performance node reports them at every station; the species list of the table is
reported so that a composition can be read in table order; mole fractions are over all
species, the reference's convention (the parent's BOOT.md). `EquilibriumProblem.Enthalpy`
is nullable so that the mixture's enthalpy is the default; `TemperatureEstimate`,
`ExitCount`, `TransportStatus` and the sweep's defaults are new.

## Solving ✅

```csharp
public sealed class Solver : IDisposable
{
    public static Solver Create(SpeciesDatabase database, EngineOptions? options = null);
    public SpeciesDatabase Database { get; }
    public AcceleratorInfo Accelerator { get; }
    public ElementalMixture Mixture(Propellant propellant, double? oxidizerToFuelRatio = null);   // b_i (mol/kg) and h_0 (J/kg)
    public IReadOnlyList<string> CandidateSpecies(IReadOnlyList<string> elements, IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null);
    public RocketResult Solve(Propellant propellant, RocketProblem problem);
    public IReadOnlyList<RocketResult> Solve(Propellant propellant, IReadOnlyList<RocketProblem> problems);
    public IReadOnlyList<RocketResult> Solve(RocketSweep sweep);
    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem);
    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems);
    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem);
    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems);
    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem);
    public IReadOnlyList<EquilibriumResult> Solve(ElementalMixture mixture, IReadOnlyList<EquilibriumProblem> problems);
    public IReadOnlyList<EquilibriumResult> SolveStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null);
    public void Dispose();
}
```

A solver owns one engine of the execution node, bound by the options to the CPU
accelerator or to CUDA. Rocket problems of one call are grouped by exit layout
(number of pressure-ratio and area-ratio exits) and each group is one batch; the
results come back in the order given. Equilibrium problems of one call are one batch.
Transport figures are evaluated in a second pass over the stations of the cases that
asked for them. The uploaded tables of every element set and species list, and the
reactant enthalpies of every propellant instance, are kept until `Dispose`.

⚠ 2026-09-12: `Database`, `Mixture` and `CandidateSpecies` were added so that a
caller (and the tests) can see the mixture a propellant implies and the species a
selection produces without solving; `Solve(ElementalMixture, IReadOnlyList<EquilibriumProblem>)`
completes the overload set; `SolveStates`' options are optional.

## Errors

| Situation | Behaviour |
|---|---|
| no reactant, an unknown reactant name | `ArgumentException`, `KeyNotFoundException` naming it, from `Build` |
| a reactant temperature outside its record's range and margin; a mixture rule with an empty or zero-mass group, or oxidizers and fuels without a ratio, or a named reactant with a ratio; a custom reactant with an element without atomic weight; an `Only` name that is no product species or lies outside the elements | `ArgumentException` naming the reactant, the element or the species, from `Build` |
| a negative amount, a non-positive temperature, an empty formula, a non-finite enthalpy | `ArgumentException` naming the reactant, from `Reactant` |
| an element symbol without a database record; a mixture without enthalpy given a rocket problem or an assigned-enthalpy problem without one; a non-positive pressure, chamber pressure or exit value; transport requested on a database loaded without `trans.inp`; an empty batch or sweep; a ratio set on a propellant given by mass fractions | `ArgumentException` naming the element or the problem index, from `Solve`, before any kernel runs |
| a state record with none or more than one of enthalpy, temperature and entropy, a negative abundance, an empty or duplicated symbol | `ArgumentException` naming the record index, from `SolveStates`, before any kernel runs |
| accelerator unavailable or ILGPU mismatch | the `Execution` exceptions, unchanged |
| per-case numerical failure | `Status` on the result and on the station; no exception |
| a disposed solver | `ObjectDisposedException` |

## Side effects

None beyond those of `Execution`: the solver creates an engine at `Create` and keeps
device copies of the tables it has built until `Dispose`.

## Out of scope

- Reading files, JSON, command-line options: `Cli`.
- Equivalence ratio, percent fuel: not in version 1.
