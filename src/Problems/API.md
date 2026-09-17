# API.md — Problems

Namespace `APThermo.Problems`. The node exposes the
propellant and problem definitions, the result records, and the solver entry points
of the library. Everything not listed here, in a package-surface section (one whose
heading carries no `(tree contract)` mark), is internal and may change without notice
(root `BOOT.md`, Delivery: Public surface); this node's own `Mixture rule` section
below is its only tree contract (the API review's finding M2, fixed in `2bca252`), read only by
this node's own tests, which need no grant (AGENTS.md §6: a mirrored test node's use
of its own source node's internals is a design invariant, not a friend crossing).
Types of the neighbours appear in the signatures: `SpeciesDatabase` and
`ElementCount` (`Data`), `MixtureState` and `CaseStatus` (`Thermo`), `ProblemKind`
(`Equilibrium`), `FlowModel` and `PerformanceFigures` (`Performance`),
`TransportFigures` (`Transport`), `EngineOptions`, `AcceleratorInfo` and
`AcceleratorProbe` (`Execution`).

## Propellants ✅

```csharp
namespace APThermo.Problems;

public enum ReactantRole { Oxidizer, Fuel, Named }      // Named: a total mass fraction, outside any oxidizer/fuel split

public enum AmountKind { MassFraction, Moles }

public sealed record CustomReactantDefinition(
    IReadOnlyList<ElementCount> Formula,          // atoms per formula unit; not empty
    double Enthalpy,                              // J/mol at Temperature; finite
    double Temperature,                           // K; positive
    double? MolarMass = null);                    // kg/kmol; null = from the formula and the atomic weights

public sealed record Reactant
{
    public const double DefaultTemperature = 298.15;    // K: a record with polynomial intervals, no temperature given
    public static Reactant FromDatabase(string name, ReactantRole role, double amount, double? temperature = null,
                                        AmountKind amountKind = AmountKind.MassFraction);
    public static Reactant Custom(string name, CustomReactantDefinition definition, ReactantRole role, double amount,
                                  AmountKind amountKind = AmountKind.MassFraction);
    public string Name { get; }
    public ReactantRole Role { get; }
    public double Amount { get; }                       // within its role group; normalized by the builder
    public AmountKind AmountKind { get; }
    public double? Temperature { get; }                 // K; null = the record's default
    public bool IsCustom { get; }
    public CustomReactantDefinition? Definition { get; }   // custom reactants only
}

public sealed record Propellant
{
    public static PropellantBuilder From(SpeciesDatabase database);
    public IReadOnlyList<Reactant> Reactants { get; }
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

⚠ 2026-09-15 (distribution phase): `MixtureSpecification` and `Propellant.Mixture`
moved into the tree-contract section below (the API review's finding M2, fixed in `2bca252`):
`Propellant.OxidizerToFuelRatio` already carries the same information without loss
for a consumer (null for a propellant given by total mass fractions), and only this
node's own tests read `Mixture` directly.

## Mixture rule (tree contract) ✅

```csharp
internal abstract record MixtureSpecification
{
    public sealed record OxidizerToFuel(double Ratio) : MixtureSpecification;
    public sealed record MassFractions : MixtureSpecification;   // the reactants' amounts are total mass fractions
}
```

`Propellant`'s internal `Mixture` property holds the `MixtureSpecification` a
`PropellantBuilder` chose: `OxidizerToFuel` when `OxidizerToFuelRatio` was called,
`MassFractions` otherwise.

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

⚠ 2026-09-14: `Reactant.Custom` took eight parameters — `formula`, `enthalpy`,
`temperature`, `role`, `amount`, `molarMass`, `amountKind` next to `name` — with two
adjacent doubles (`enthalpy`, `temperature`) a caller could swap without a compiler
error, and the properties `Formula`, `Enthalpy` and `MolarMass` existed only on a
custom reactant, null otherwise (the clean-code review's F-PR-05). The four values
that exist only together now travel as one `CustomReactantDefinition`, which
`Reactant.Definition` exposes in their place; the factory drops to five parameters.

## Elemental mixtures and state records ✅

A mixture may be given without reactants, by its element abundances and enthalpy per
kilogram: the form another simulation hands over, and the form the batch path is
built for.

```csharp
public sealed record ElementalMixture
{
    public const double DefaultMassTolerance = 1.0e-2;  // relative to one kilogram: the tolerance a mixture declares when it names none (BOOT.md)
    public static ElementalMixture Create(IReadOnlyDictionary<string, double> elementMoles,   // mol per kg
                                          double? enthalpy = null,                             // J per kg; null: assigned-temperature problems only
                                          IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null,
                                          double massTolerance = DefaultMassTolerance);        // finite and non-negative, else ArgumentException naming it
    public IReadOnlyDictionary<string, double> ElementMoles { get; }   // symbols in the database spelling ("Al" → "AL")
    public double? Enthalpy { get; }
    public IReadOnlyList<string> Elements { get; }                    // order used in results
    public IReadOnlyList<string> Omit { get; }
    public IReadOnlyList<string>? Only { get; }
    public double MassTolerance { get; }                              // the tolerance this mixture declares; the solver compares |Σ n_i A_i − 1| with it
    public static bool IsValidMassTolerance(double massTolerance);   // finite and non-negative: the one statement of the rule `Create` applies
}

public sealed record StateRecord(                     // the exchange record: one state of a mixture
    double Pressure,                                  // Pa; the chamber pressure when HasExits
    IReadOnlyDictionary<string, double> Composition,  // element moles, mol per kg
    double? Enthalpy = null,                          // J/kg  → hp problem, or every record with exits
    double? Temperature = null,                       // K     → tp problem
    double? Entropy = null)                           // J/(kg·K) → sp problem; exactly one of the three is set
{
    public IReadOnlyList<double> AreaRatios { get; init; }       // supersonic A / A_t, reported after the pressure-ratio exits; empty by default
    public IReadOnlyList<double> PressureRatios { get; init; }   // p_c / p_e, reported first; empty by default
    public FlowModel? Flow { get; init; }                        // records with exits only; null = shifting equilibrium
    public bool HasExits { get; }                                // AreaRatios or PressureRatios not empty: a rocket case through SolveRocketStates
}

public sealed record StateBatchOptions(bool Transport = false, IReadOnlyList<string>? Omit = null, IReadOnlyList<string>? Only = null,
                                       double MassTolerance = ElementalMixture.DefaultMassTolerance);   // declared for every record of the batch

public sealed class StateRecordException : ArgumentException   // a state record refused by a rule of its shape; not the mass rule (MixtureMassException)
{
    public StateRecordException(int index, string reason);
    public int Index { get; }                                    // the record's position in the list given
    public string Reason { get; }                                // the message without the subject
}

public sealed class MixtureMassException : ArgumentException   // the solver's refusal of a mixture beyond its MassTolerance
{
    public MixtureMassException(string subject, int index, double mass, double tolerance);
    public int Index { get; }                         // position in the batch: the case index, or the state record's index
    public double Mass { get; }                       // kg: Σ n_i A_i with the database's atomic weights
    public double Tolerance { get; }                  // the tolerance in force, relative
    public string Reason { get; }                     // the message without the subject, for a caller that names the mixture its own way
}
```

An `ElementalMixture` is accepted everywhere a `Propellant` is (rocket and
equilibrium problems). A list of `StateRecord`s is one batch: every record may carry
its own composition, pressure and target; the candidate species are chosen from the
union of the elements of the batch (in order of first appearance) under the options'
lists; an element absent from a record (or zero) makes the species containing it
inactive for that record. Element moles are converted once, here, to the kmol per kg
the numerical nodes use.

A record naming an exit (`AreaRatios` or `PressureRatios` not empty, `HasExits` true)
is a rocket case: its `Pressure` is the chamber pressure, its enthalpy is required,
and a `Flow` may be named only on such a record; it solves through
`Solver.SolveRocketStates`, and a record without exits through `Solver.SolveStates`
(F-AR-02, decided at the root, because the command line had re-decided these rules of
the state record itself, and nobody owned the shape). A record refused by a rule of
its shape (none or several of enthalpy, temperature and entropy given; exits without
an enthalpy; a flow named without exits; a record given to the batch method that does
not take its kind; a negative abundance, an empty or duplicated symbol) throws a
`StateRecordException` whose message is `state record i: ` followed by its `Reason`.

Element moles are per kilogram of mixture, and the solver holds every mixture to it
before any kernel runs: their mass with the database's atomic weights must be one
kilogram within the tolerance the mixture declares (`MassTolerance`, given at
`Create` or through `StateBatchOptions`, `DefaultMassTolerance` when none is named;
the propellant path always declares the default, since its mixture is the tree's own
and a deviation there is a database defect, not the caller's knowledge), else the
solve throws a `MixtureMassException` whose message is the subject and the reason,
`state record 3: the composition weighs 2000.03 g with the database's atomic weights;
element moles are per kilogram of mixture, so it must weigh 1000 g within 1 %` (the
subject is `mixture i` from the overloads over mixtures, `state record i` from
`SolveStates` and from `SolveRocketStates`, and `the propellant's mixture (case i)`
when a reactant record's molar mass contradicts its formula; the tolerance in force is
printed in percent to three significant digits). The mass itself is reported:
`Solver.MassOf` gives it for any mixture, and every result carries it as
`MixtureMass`, so that a raised tolerance never hides the figure.

⚠ 2026-09-13: the contract said "mol per kg" and checked nothing: a record with every
element mole doubled, or in mol/g, solved without a word (the parent's BOOT.md,
invariants). The check and `MixtureMassException` were added with a constant
`MassTolerance`; the design session of the same day made the tolerance a declaration
of the mixture (the constant became `DefaultMassTolerance`, the mixture gained
`MassTolerance`, the options and the exception their fields) and the mass a figure of
the result (`MixtureMass`, `MassOf`). The decisions taken then, so that they are not
reopened by accident: no "warning" mode (a record solved as given is wrong in every
per-kilogram figure, and a flag left in a script lets the next thousandfold error
through); no normalization of the moles to one kilogram (it would guess the basis of
the enthalpy, and the reference's own `b_i` are not normalized); the tolerance
travels with the mixture, not with a problem or a document field, because it
describes the caller's records, not the physics. The reasons are in the parent's `BOOT.md`.

⚠ 2026-09-12: the sketch's `Create(elementMoles, enthalpy)` had a mandatory enthalpy
and no species lists. A mixture solved only at assigned temperatures has no enthalpy
to give, and the lists belong to the mixture so that a state batch and a propellant
select species the same way.

⚠ 2026-09-14: the tolerance rule (finite and non-negative) was stated in this node's
`Create` and, separately, in the command line's own option parsing, each with its own
message (the clean-code review's open question 2). `ElementalMixture.IsValidMassTolerance`
is now the one statement of the rule; `Create` refuses through it, and any other
caller that wants to check a tolerance before offering it (the command line keeps its
own message, since it also refuses text that is no number) may call it too.

⚠ 2026-09-14: `StateRecord` had no way to carry an exit, so the command line
re-decided, on its own, which of its five fields a rocket case needed and which an
equilibrium one did not (F-AR-02, the architecture review). The record keeps its five
positional parameters — no construction site changes — and gains `AreaRatios`,
`PressureRatios` and a nullable `Flow` as init properties, with `HasExits` the one
statement of the kind rule; `StateRecordException` was added the same day so that a
refused record's index and reason travel like `MixtureMassException`'s, instead of a
generic `ArgumentException`.

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

public sealed record Station               // internal constructor (M1): a consumer only reads one; every property is init, for this node's own tests
{
    public string Name { get; init; }                                        // "chamber", "throat", "exit1", "exit2", …; "state" for an equilibrium result
    public MixtureState State { get; init; }                                 // zero where the status is not Ok
    public PerformanceFigures? Performance { get; init; }                    // rocket stations only
    public IReadOnlyDictionary<string, double> MoleFractions { get; init; }  // every species of the table, n_j over the moles of all species
    public IReadOnlyDictionary<string, double> CondensedMassFractions { get; init; }   // every condensed species, n_j M_j
    public TransportFigures? Transport { get; init; }                        // null when not requested or not Ok
    public CaseStatus? TransportStatus { get; init; }                        // null when transport was not requested
    public CaseStatus Status { get; init; }
}

public sealed record RocketResult          // internal constructor (M1): a consumer only reads one
{
    public Propellant? Propellant { get; }                             // null for an elemental mixture
    public ElementalMixture Mixture { get; }                           // the element moles and enthalpy the case started from
    public double MixtureMass { get; }                                 // kg: Σ n_i A_i of those element moles with the database's atomic weights
    public RocketProblem Problem { get; }
    public double? OxidizerToFuelRatio { get; }                        // the ratio of the mixture rule, or null
    public IReadOnlyList<string> Species { get; }                      // table order: gases, then condensed species
    public IReadOnlyList<Station> Stations { get; }                    // chamber, throat, exits in order
    public CaseStatus Status { get; }
    public AcceleratorInfo Accelerator { get; }
}

public sealed record EquilibriumResult     // internal constructor (M1): a consumer only reads one
{
    public Propellant? Propellant { get; }
    public ElementalMixture Mixture { get; }
    public double MixtureMass { get; }                                 // kg, as on RocketResult
    public EquilibriumProblem Problem { get; }
    public IReadOnlyList<string> Species { get; }
    public Station State { get; }
    public CaseStatus Status { get; }
    public AcceleratorInfo Accelerator { get; }
}
```

⚠ 2026-09-15 (distribution phase): `Station`, `RocketResult` and `EquilibriumResult`
had public positional constructors that no other assembly called (the API review's
finding M1, fixed in `2bca252`): a consumer only ever reads one of these records from
a solve. They are nominal now, with an internal constructor; `Station`'s properties
are `init` so that this node's own tests can build a comparison copy with `with`
(`tests/Problems.Tests/EquilibriumTests.cs`), `RocketResult`'s and
`EquilibriumResult`'s are get-only. A field added in 0.x now breaks no consumer.

⚠ 2026-09-14: `RocketSweep` and `Solver.Solve(RocketSweep)` are retired (the
clean-code review's F-PR-06): a `RocketSweep` expanded to every (ratio, chamber
pressure) pair, all in one batch, results ratio-major then by pressure — but it built
its system from its first case's mixture alone, bypassing the union-of-mixtures path,
a second implementation of one rule with its own behaviour and one caller. The same
product is now a list of mixtures (one `Solver.MixtureOf` call per ratio) with a list
of problems (one per pressure), through `Solver.Solve(IReadOnlyList<ElementalMixture>, IReadOnlyList<RocketProblem>)`,
the batch mechanism every other list of mixtures uses; the command line expands its
own sweeps (ratio, pressure and temperature, rocket and equilibrium) and calls that
overload, rather than reading a rule of the library's that existed for one caller.

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

Since 2026-09-13: when the species table cuts a condensed
record at a fit discontinuity (the Thermo node's join-and-cut rule; `ALN(L)` today),
`Species`, `MoleFractions` and `CondensedMassFractions` carry the record's name once,
the pieces summed — no piece name leaves this node (the parent `BOOT.md`, results).

⚠ 2026-09-14: a failed station's `MoleFractions` and `CondensedMassFractions` had no
stated meaning (the clean-code review's open question 5). They are computed the same
way as an `Ok` station's, from the moles the numerical nodes left at that station when
they stopped: no solution, offered for diagnosis only, never pinned by a test.

## Solving ✅

```csharp
public sealed class Solver : IDisposable
{
    public static Solver Create(SpeciesDatabase database, EngineOptions? options = null);
    public SpeciesDatabase Database { get; }
    public AcceleratorInfo Accelerator { get; }
    public ElementalMixture MixtureOf(Propellant propellant, double? oxidizerToFuelRatio = null);   // b_i (mol/kg) and h_0 (J/kg)
    public IReadOnlyList<string> CandidateSpeciesFor(IReadOnlyList<string> elements, IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null);
    public double MassOf(ElementalMixture mixture);     // kg: Σ n_i A_i with the database's atomic weights, the number the mass check compares with one kilogram
    public RocketResult Solve(Propellant propellant, RocketProblem problem);
    public IReadOnlyList<RocketResult> Solve(Propellant propellant, IReadOnlyList<RocketProblem> problems);
    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem);
    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems);
    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem);
    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems);
    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem);
    public IReadOnlyList<EquilibriumResult> Solve(ElementalMixture mixture, IReadOnlyList<EquilibriumProblem> problems);
    public IReadOnlyList<RocketResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems);           // one case per index
    public IReadOnlyList<EquilibriumResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems);
    public IReadOnlyList<EquilibriumResult> SolveStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null);           // records without exits
    public IReadOnlyList<RocketResult> SolveRocketStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null);          // records with exits
    public void Dispose();
}
```

A solver owns one engine of the execution node, bound by the options to the CPU
accelerator or to CUDA. Rocket problems of one call are grouped by exit layout
(number of pressure-ratio and area-ratio exits) and, within a layout, by the transport
flag, so the transport pass runs only over the cases of that layout that asked for it
(F-PR-08); the results come back in the order given. Equilibrium problems of one call
are grouped by the transport flag the same way. A list of mixtures with a list of
problems is one batch with one case per index over the union of the mixtures'
elements; every mixture must carry the same `Omit` and `Only` lists, and `SolveStates`
and `SolveRocketStates` are that overload over state records without and with exits
respectively (the elemental-mixtures section). The uploaded tables of every element
set and species list, and the reactant enthalpies of every propellant instance, are
kept until `Dispose`.

Every public method throws `ObjectDisposedException` once the solver is disposed,
`Database` and `Accelerator` excepted: they name no device resource and stay readable.

⚠ 2026-09-12: `Database`, `Mixture` (renamed `MixtureOf` on 2026-09-14, F-PR-13) and
`CandidateSpecies` (renamed `CandidateSpeciesFor` the same day) were added so that a
caller (and the tests) can see the mixture a propellant implies and the species a
selection produces without solving; `Solve(ElementalMixture, IReadOnlyList<EquilibriumProblem>)`
completes the overload set; `SolveStates`' options are optional. The two overloads over
lists of mixtures were added on 2026-09-13 for the command line: a sweep over the
oxidizer-to-fuel ratio with any exit layout, and the records of another simulation with
exits, are one batch through them. `MassOf` was added the same day with the mass
check (the elemental-mixtures section).

⚠ 2026-09-14: two defects of this contract, both from the clean-code review, fixed in
the same commit as the renames above:

- the transport pass did not match "a second pass over the stations of the cases that
  asked for them" (F-PR-08): a batch in which one case asked for transport ran the
  pass over every station of the batch, and the figures of the cases that did not ask
  were dropped afterwards. The reported figures were right, the work was not; the
  runners now group by the transport flag as they group by exit layout, so the pass
  itself is narrowed;
- the disposed-solver guard did not hold for every method (F-PR-09): `CandidateSpecies`
  and `MassOf` answered on a disposed solver instead of throwing, and three of the
  `Solve` overloads refused only because a method they called (`Mixture`) happened to
  check. Every public method now checks for itself.

## Errors

| Situation | Behaviour |
|---|---|
| no reactant, an unknown reactant name | `ArgumentException`, `KeyNotFoundException` naming it, from `Build` |
| a reactant temperature outside its record's range and margin; a mixture rule with an empty or zero-mass group, or oxidizers and fuels without a ratio, or a named reactant with a ratio; a custom reactant with an element without atomic weight; an `Only` name that is no product species or lies outside the elements | `ArgumentException` naming the reactant, the element or the species, from `Build` |
| a negative amount, a non-positive temperature, an empty formula, a non-finite enthalpy | `ArgumentException` naming the reactant, from `Reactant` |
| an element symbol without a database record; a mixture without enthalpy given a rocket problem or an assigned-enthalpy problem without one; a non-positive pressure, chamber pressure or exit value; transport requested on a database loaded without `trans.inp`; an empty batch; a ratio set on a propellant given by mass fractions | `ArgumentException` naming the element or the problem index, from `Solve` and `MixtureOf`, before any kernel runs |
| a state record with none or more than one of enthalpy, temperature and entropy; a record with exits given to `SolveStates` or one without exits given to `SolveRocketStates`; exits without an enthalpy; a flow named without exits; a negative abundance, an empty or duplicated symbol | `StateRecordException` (an `ArgumentException`) naming the record's index and the reason, from `SolveStates` and `SolveRocketStates`, before any kernel runs |
| a mixture whose element moles weigh more or less than one kilogram with the database's atomic weights by more than the mixture's `MassTolerance` (a doubled record, mol/g, kmol/kg, a reactant record whose molar mass contradicts its formula) | `MixtureMassException` (an `ArgumentException`) naming the mixture (`mixture i`, `state record i`, the propellant's mixture), the mass in grams and the tolerance in force, from `Solve`, `SolveStates` and `SolveRocketStates`, before any kernel runs |
| a mass tolerance that is negative or not finite | `ArgumentException` naming `massTolerance`, from `Create` |
| accelerator unavailable or ILGPU mismatch | the `Execution` exceptions, unchanged |
| per-case numerical failure | `Status` on the result and on the station; no exception |
| a disposed solver | `ObjectDisposedException`, from every public method; `Database` and `Accelerator` stay readable |

## Side effects

None beyond those of `Execution`: the solver creates an engine at `Create` and keeps
device copies of the tables it has built until `Dispose`.

## Out of scope

- Reading files, JSON, command-line options: `Cli`.
- Equivalence ratio, percent fuel: not in version 1.
