# BOOT.md — Problems

## Purpose

The front door of the library: propellants and reactants, the assembly of the
chemical system (elements, candidate species, element moles and enthalpy per kilogram
of propellant), the problem and result types a user works with, and the
orchestration of `Data`, `Thermo`, `Transport` and `Execution` into one call, with the
result types of `Performance`. The conventions here (what a reactant is, how an
oxidizer-to-fuel ratio becomes mass fractions, which species are candidates) are
knowledge about propellants and about NASA CEA's habits, not about solving, which is
why they are a node of their own.

## Invariants

- **The species set of a batch is fixed by the element set and the species lists**,
  not by the amounts: the candidate list depends only on the elements present in the
  union of the reactants, or in the union of the elemental records of a state batch,
  and on the `Omit` or `Only` list; changing amounts, pressures or exits never changes
  the table. Cases of one batch share one `SpeciesTable`; a case in which an element
  is absent runs with the species containing it inactive (see the `Equilibrium`
  contract), not with another table.
- **Two front doors, one path.** A mixture given by reactants and a mixture given by
  element moles and enthalpy meet in the same `ElementalMixture` before anything else
  happens; every solve starts from element moles per kilogram and an enthalpy, whatever
  the input was.
- **Element moles describe one kilogram.** Whatever the front door, the mass of the
  element moles with the database's atomic weights, `Σ n_i A_i`, is one kilogram
  within the mixture's mass tolerance, or the solve refuses the mixture before any
  kernel runs with a `MixtureMassException` naming it (`state record i`,
  `mixture i`, the propellant's mixture), the mass found in grams and the tolerance.
  The tolerance is a declaration of the mixture (`MassTolerance` per instance, given
  at `Create` or through `StateBatchOptions`; `DefaultMassTolerance` = 1e-2 when none
  is named; the propellant path always at the default, since its mixture is the
  tree's own), and the mass itself is reported on every result (`MixtureMass`) and by
  `Solver.MassOf`, so that a raised tolerance never hides the figure. The default is
  derived from what must pass and what must fail:
  - must pass: a record built with the database's own atomic weights differs from
    one kilogram by the rounding of its digits (the record of 2026-09-13 below:
    1.5e-5); a record built from a reactant record's own molar mass carries that
    record's rounding (the element moles of every fixture file, 195 on 2026-09-13,
    are within 1.7e-5 of one kilogram, the largest 1.6502e-5 from the `Air` record's
    28.9651159 kg/kmol against its formula's 28.96561, a bound the front door tests
    node checks over the directory listing; the reactant records `HAN` and
    `LMP-103S` are rounded by 4.5e-4 and 1.5e-4); a record built with another
    atomic-weight table differs by that table's last digits (below 1e-4); a
    simulation that omits its trace elements loses their mass, and an element worth
    more than a percent of the mass is no trace;
  - must fail: the nearest plausible mistakes are a composition per kilogram of one
    reactant instead of the mixture (LOX/LH2 at an oxidizer-to-fuel ratio of 6: 17 %
    off per kilogram of oxidizer, sixfold per kilogram of fuel), per pound (0.4536 kg,
    54 % off), per two kilograms (100 %), in mol/g or kmol/kg (a thousandth), in
    mmol/kg (a thousandfold), per 100 g (tenfold);
  - so any tolerance between 1e-2 and 0.17 separates the two; it is set at the lower
    end, 1e-2, so that the largest legitimate deviation passes and anything larger,
    which is no longer a trace omission but a different mixture, fails. The nearest
    plausible mistake is seventeen times the tolerance; the largest legitimate
    deviation measured, the `HAN` record's rounding of 4.5e-4, is twenty-two times
    below it.

  ⚠ 2026-09-13: until this date nothing checked the mass. A record of another
  simulation (C, H, O, N, Cl, Al; 1000.015 g) solved to 2701.37 K at 6.5 MPa, and
  the same record with every element mole doubled solved without a message to
  2799.63 K; a composition in mol/g or kmol/kg passed the same way, against the
  root's intent and the command line's promise that a unit mistake cannot pass
  silently. The propellant path is held to the same check: a reactant record whose
  molar mass contradicts its formula yields a mixture of the wrong mass, and the
  committed file has one (`ADN`, 630.0 kg/kmol against its formula's 124.06), so
  such a propellant is refused instead of being solved for a fifth of a kilogram.

  2026-09-13, design session, the decisions behind the declared tolerance. The check
  knows `Σ n_i A_i`; only the caller knows why a record deviates: trace elements
  omitted (a few percent, always a deficit), another atomic-weight table (about 1e-4,
  but up to 0.85 % for lithium and 0.14 % for boron, whose natural abundances vary),
  a reactant record's rounding (4.5e-4). A declared tolerance lets the caller state
  that knowledge and keeps the check for everything beyond it. A "warning" mode
  would not: it would solve the record as given, and such a solution is wrong in
  every per-kilogram figure (a tp state keeps its mole fractions, but molar mass,
  density, enthalpy, entropy and heat capacities carry the factor; an hp state's
  temperature carries it too, 2701 K against 2800 K for the doubled record), and once
  set in a script it lets the next thousandfold error through with exit code 0, which
  is the silence the check exists to end. A normalization of the moles to one
  kilogram is not offered either: it would guess the basis of the enthalpy, and the
  reference's own `b_i` are not normalized (the fixtures deviate by up to 1.65e-5
  because the reference divides by each reactant record's molar mass), so a default
  normalization would move the tree off the reference. The user's records are per
  kilogram; should records that are proportions ever appear, a declared basis is the
  honest feature, not a warning. Coded the same day; the tests of the declared
  tolerance use the record made heavy, because made light it does not converge: at
  6.5 MPa its enthalpy sits 1.4 K above the 2700 K interval boundary of the `ALN(L)`
  record, across which the tree's enthalpy of the mixture jumps by 357 kJ/kg (the
  record's two fits differ by 68 kJ/mol there), and an assigned enthalpy inside that
  gap has no solution on either side: the equilibrium node's open defect at a
  transition with variable temperature, in a new place. Recorded here because it was
  found here; the fix is that node's. 2026-09-13, the design sessions of the same
  day assigned the fix: the Thermo builder cuts such a record at its jump
  (join-and-cut) and the equilibrium node holds a pinned pair there, so an assigned
  enthalpy inside the gap settles on the 2700 K plateau — coded the same day: the
  record made light solves through the front door
  (`SplitRecordTests.An_enthalpy_inside_the_ALN_gap_solves_through_the_front_door`)
  and the gap is closed.
- **Candidate species are chosen by one rule**: every gaseous product species of the
  database whose elements are all among the mixture's elements, then every condensed
  product species under the same condition, each in database order, minus the `Omit`
  list, or exactly the `Only` list when given; ionized species (the electron
  pseudo-element `E` in the formula) and inert pseudo-element records are never
  candidates in version 1; a name with several records (a condensed species with one
  record per temperature range) is one candidate. An omitted name that is no product
  species is ignored, as the reference ignores it: the RP-1311 example 3 omit list
  names reactant-only species and old spellings.

  ⚠ 2026-09-12: stood "ionized species (names ending in `+` or `-`, and `e-`)". A
  trailing sign is no criterion: the database truncates names such as `C3H4,cyclo-`,
  and forty neutral species end in `-`. The end-to-end comparison reported them "not
  in the table" while the reference's product lists carried them. An ion carries `E`
  in its formula, and that is what the rule reads.
- **Element moles and enthalpy are computed from the database records**, per
  kilogram of propellant: `b_i = Σ_k w_k a_ik / M_k`, `h_0 = Σ_k w_k H_k(T_k) / M_k`,
  with `H_k(T_k)` from the record's polynomial at the reactant's temperature,
  evaluated through the execution node's species-function batch (the tree's one
  implementation of the species functions lives in `Thermo` and runs on an
  accelerator), or the assigned enthalpy for records without intervals and for custom
  reactants. A record with intervals defaults to `Reactant.DefaultTemperature`
  (298.15 K), a record without intervals to its assigned temperature. A reactant
  temperature is accepted within the record's range widened by
  `PropellantBuilder.TemperatureMargin` (10 K) on either side, and the nearest
  interval is evaluated there; beyond it the reactant is rejected by name.

  ⚠ 2026-09-12: stood "a reactant temperature outside the record's range is an error,
  not an extrapolation". The reference's own notion of a record's valid range is the
  fit range, or the assigned temperature ± 10 K for a record without fits, and it
  evaluates the polynomial without a range check: the AP/HTPB/Al fixtures carry
  `AL(cr)` at 298.15 K against its 300 K lower bound, and a strict rule would reject
  the reference's own inputs. The margin mirrors the reference's ± 10 K.
- **Amounts are mass based.** Oxidizer and fuel amounts within their group are
  normalized to one; the oxidizer-to-fuel ratio splits the kilogram as
  `w_ox = OF / (1 + OF)`, `w_fuel = 1 / (1 + OF)`; a propellant given by total mass
  fractions is used as given after normalization to one; mole amounts are converted
  to mass with the record's molar mass before anything else.
- **SI in, SI out, names out.** Public types carry SI units and species names; no
  index leaves this node.
- **Statuses become results or exceptions, once.** A per-case failure is a
  `CaseStatus` in the result record; an infrastructure failure is an exception from
  `Execution` passed through; nothing is retried silently.
- **Immutable inputs.** Propellants and problems are immutable records; a solve never
  mutates them.

## Dependencies

- [Data](../Data/API.md) — the species database and atomic weights.
- [Thermo](../Thermo/API.md) — table building, `MixtureState`, `CaseStatus`, the gas constant.
- [Equilibrium](../Equilibrium/API.md) — `ProblemKind`, the kind of an equilibrium problem.
- [Performance](../Performance/API.md) — `FlowModel`, `ExitSpecification`, `PerformanceFigures`.
- [Transport](../Transport/API.md) — transport table building and `TransportFigures`.
- [Execution](../Execution/API.md) — the engine, the batch containers and the species-function batch.

Outside the tree: the .NET base class library.

⚠ 2026-09-12: the root's decomposition listed this node's dependencies without
`Equilibrium`. The kind of an equilibrium problem is `Equilibrium`'s `ProblemKind`,
which the execution node's batch takes and this node's `EquilibriumProblem` exposes;
a link is truer than a retold enum, and the root records the same.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Ordinary .NET code; the only allocations of a solve happen here and in `Execution`.
- Reactants are database records by name, or custom reactants given by name, formula
  (element counts), molar mass (derived from the formula and the atomic weights when
  not given), enthalpy at a temperature (J/mol), and that temperature. This is how
  binders such as HTPB are defined, exactly as CEA's exploded-formula reactants.
- Mixture specifications supported in version 1: oxidizer-to-fuel ratio, total mass
  fractions, per-reactant moles (converted to mass). Equivalence ratios and percent
  fuel are not in version 1 (they need element valences typed into code, which the
  root forbids; a later version may read them from a data file).
- The database is loaded by the caller and passed in; this node never opens files.
- Element order of a chemical system: the order of first appearance in the reactants
  (oxidizers, then fuels, then named reactants, each in the order given), or across
  the records of a state batch; species order: gaseous species in database order,
  then condensed species in database order. Both are reported in the result, because
  compositions are returned by name.
- Results carry the composition of every station as mole fractions over all species
  of the table (`n_j` over the sum of the moles of gaseous and condensed species, the
  reference's convention) and, for condensed species, also as mass fractions
  `n_j M_j`, both by name, without a threshold; thresholds are a presentation concern
  of the command line.

  ⚠ 2026-09-12: stood "mole fractions of the gaseous phase and mass fractions of
  condensed species". The reference reports mole fractions over all species, and a
  result that compares to it without a conversion is worth more than a gas-phase
  convention nobody asked for; the condensed mass fractions stay.

  2026-09-13 (coded the same day): when the species table cuts a
  condensed record at a fit discontinuity (the Thermo node's join-and-cut rule;
  `ALN(L)` today), the result speaks the record's name — the mole fractions and
  condensed mass fractions of the pieces are summed under it and `Species` lists it
  once. The pieces are one substance, cut only so that the solver sees the fit's
  jump as a transition; no piece name leaves this node.
- Batch construction: one propellant definition (reactant set and temperatures) with
  per-case amounts (oxidizer-to-fuel ratio or mass fractions), chamber pressure and
  exit values; the number of exits per batch is fixed by the batch, the values vary
  per case. Rocket problems with different exit layouts given in one call are grouped
  by layout, one batch per group, and the results come back in the order given. Several
  mixtures with one problem each (rocket or equilibrium) are one batch over the union of
  their elements when their species lists agree. In such a batch the results of a case
  equal those of the case solved with its own table to rounding: bit for bit, the
  transport figures included, when the union (elements in order of first appearance)
  keeps the relative order of the case's elements; within 1e-9 relative when it
  reorders them, because the linear solves pivot in element order and the iterates
  then differ by rounding at every step. The transport set of a case counts the gases
  of the case, not of the table (the transport node's `BOOT.md`, 2026-09-13), so the
  larger table changes no figure. A
  state batch is a list of records, each with its own element moles, pressure and one
  target (enthalpy, temperature or entropy); its element set is the union over the
  records. A record with exits (area ratios or pressure ratios) is a rocket case: its
  pressure is the chamber pressure, its enthalpy is required, and a flow model may be
  named only on such a record; `SolveRocketStates` solves the records with exits and
  `SolveStates` those without, each call one batch (2026-09-14: the state record is
  this node's exchange shape, and the command line reads it without deciding a rule of
  its own; decided at the root on the architecture review's F-AR-02).
- Units at this boundary: element abundances are accepted in mol per kg and passed
  to the numerical nodes in kmol per kg (the CEA convention); enthalpy in J/kg is
  passed unchanged. The node's unit factors are two named constants
  (`UnitFactors.MolesPerKilomole`, `UnitFactors.GramsPerKilogram`), and the
  conversions it makes are three: the abundances above; a record's assigned enthalpy
  from J/mol to J/kmol on its way to the per-kilogram sum; kilograms to grams in the
  mass message.

  ⚠ 2026-09-14: stood "the one conversion this node makes besides mass
  normalization". The code held three, written as bare `1.0e3` and `1.0e-3` at five
  sites (the clean-code review's F-PR-10): an absolute word without proof, the kind
  `AGENTS.md` §8 names. Corrected to the list above, with the constants.
- The mass tolerance is a declaration about the input, not a physical quantity: it
  travels with the mixture (2026-09-13), never with a problem, and the command line
  passes it as a run option, not as a field of a document.
- Element symbols are matched to the database spelling case-insensitively (`Al`,
  `al` and `AL` are the same element); the result reports the database spelling.
- Single-case calls are batches of one.
- The solver keeps the uploaded tables of every element set and species list it has
  seen, and the reactant enthalpies of every propellant instance, until it is disposed.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). `Solver`
was one class of 663 lines with an efferent coupling of 34, holding eleven
responsibilities (the review's F-PR-01); `PropellantBuilder.Build` validated the
mixture rule and derived the element order in 77 lines (F-PR-03). The data flow is
`Solver` → `PropellantMixtures` (the propellant front door only) →
`ChemicalSystemCache` → `ProblemValidation` and `MixtureMass` per case →
`RocketRunner` or `EquilibriumRunner` → the engine → `StationFactory` → the result
records. Every type below is internal except where marked; one type per file, named
after the type; the public records keep their theme files.

| Type | Responsibility | Visibility |
|---|---|---|
| `Solver` | the composition root: owns the engine and the collaborators below, turns each public entry point into (system, cases) and hands them to a runner; holds no rule. The declared exception to the coupling limit: it names the public problem and result types, the engine and its collaborators. Ce = 22 (`AcceleratorInfo`, `ChemicalSystem`, `ChemicalSystemCache`, `ElementalMixture`, `Engine`, `EngineOptions`, `EquilibriumCase`, `EquilibriumProblem`, `EquilibriumResult`, `EquilibriumRunner`, `MixtureMass`, `Propellant`, `PropellantMixtures`, `RocketCase`, `RocketProblem`, `RocketResult`, `RocketRunner`, `SpeciesDatabase`, `SpeciesSelection`, `StateBatchOptions`, `StateRecord`, `StateRecords`), measured 2026-09-14 after the contract commit (a manual signature-and-body count, `RocketSweep` dropping out with its removal), down from 23 after the internal decomposition and 34 before either; the dependency check's own IL walk (fields read and members called, not only signatures) agrees at 22, run on this node's build at `ef54a4a` with the root's scratch tool. The reason for the declared exception is what it names, not a superlative: the two runners are declared composition roots as well (the decision "The runners are the pipelines' composition roots") | public |
| `ChemicalSystem` | one element set with its table and its uploaded copy; disposable; no transport table kept (F-PR-12) | internal |
| `ChemicalSystemCache` | an element list, or a list of mixtures, plus `Omit`/`Only` → a `ChemicalSystem`, built once per key (the union over mixtures, the agreement of their lists) and disposed with the solver | internal |
| `MixtureMass` | Σ n_i A_i with the database's atomic weights, the refusal beyond the mixture's declared tolerance, and the subject a refusal names (`Subject`), stated once for both runners | internal static |
| `UnitFactors` | `MolesPerKilomole` and `GramsPerKilogram`, the node's two unit constants with their origin | internal static |
| `AtomicWeights` | a missing atomic weight as an `ArgumentException` naming the element, for the element check of a chemical system and the mass of a mixture (F-PR-07); a custom reactant's resolution translates the same miss naming the reactant too (`ReactantResolver`) | internal static |
| `PropellantMixtures` | the propellant → `ElementalMixture` map: mass fractions, b_i, h_0, the reactant-enthalpy cache and its species-function batch; the piece of a cut record at a temperature is asked of the table (`SpeciesTable.PieceOf`, the Thermo node's; F-AR-01) | internal |
| `ProblemValidation` | every "before any kernel runs" rule of a rocket and of an equilibrium problem; the subject of a refusal is a field of the case, not a defaulted parameter | internal static |
| `StateRecords` | state records → mixtures and problems, and the rules of the shape: exactly one target; exits need an enthalpy; a flow only with exits; `SolveStates` takes no record with exits and `SolveRocketStates` none without; every refusal of a record (these rules, a negative abundance, an empty or duplicated symbol) is a `StateRecordException` with the record's index and a subject-free reason; the mass check keeps `MixtureMassException` | internal static |
| `RocketRunner` | the composition root of the rocket pipeline: rocket cases grouped by exit layout and by the transport flag, the batch filled by field copy, the two engine runs, the stations assembled through `StationFactory`; the transport pass runs only over the cases that asked (F-PR-08); holds no formula. The declared exception to the coupling limit (the decision "The runners are the pipelines' composition roots", its figure under `## Shape exceptions`) | internal |
| `EquilibriumRunner` | the same for equilibrium cases, with the same declared exception | internal |
| `StationFactory` | one station from one slice of the engine's flat result, and the species-name list with the cut pieces summed under the record's name; the station names from `RocketLayout.FixedStations` (F-PR-11); the transport status and figures a station reports (`TransportOf`), stated once for both runners | internal static |
| `StationSlice` | one station of the engine's flat result, the input of the one construction site of `Station`, the flat offset computed once | internal readonly record struct |
| `ReactantResolver` | one `Reactant` → one resolved reactant: the database and custom paths as two named methods, the temperature default and the margin, the formula spelling, the molar mass, the amount → mass conversion | internal static |
| `MixtureRule` | the role composition and the ratio guard (its one owner, F-PR-07), the `MixtureSpecification`, and the kilogram split (`MassFractions`, moved off `Propellant`, which stays a definition record) | internal static |
| `PropellantBuilder.Build` | the guard clause, resolving and splitting reactants by role, the mixture rule, the element order (`ElementOrder.OfFirstAppearance`, shared with `ChemicalSystemCache.Union`) and the `Only` validation, then the constructor: a sequence of calls, no loop of its own, nesting 1 | public, unchanged |

⚠ 2026-09-15: the `Solver` row's last sentence stood "`RocketRunner` (Ce = 27) and
`EquilibriumRunner` (Ce = 25) measure higher by the same walk, both over the textual
limit of 10 and the walk-calibrated 14 the root records as of the integration branch's
`9facd7f`; not this node's declaration to make, left to the design session after the
merge. The two runners' Ce moved by one each, after this row was first measured, when
their `SolveGroup` dropped from nine parameters to four (the parameter fix below): a
`SolveContext` record struct now carries what `SolveGroup` used to take by six separate
parameters, and it is one more type in each runner's own vocabulary". Stale since the
design session actually held (the decision "The runners are the pipelines' composition
roots" below, and the `## Shape exceptions` rows of 26 and 24): the sentence still read
as if the runners' coupling were undecided and cited the pre-merge scratch figures
(27/25) and the superseded textual limit (10), duplicating — and disagreeing with —
what the rest of this document already states correctly. Found by the clean-code
repair review (R-Problems-9); cut to a cross-reference instead of retold, per
AGENTS.md SS8 ("claims about a foreign node… go stale without the author's knowledge;
link instead of retelling"), here applied to a claim about a different part of the
same document.

Decisions taken with the review of 2026-09-14. The contract-moving ones are coded in
the contract commit, after the internal moves: `API.md` rewritten with these as real
✅ blocks and ⚠ corrections where the old declarations stood, and
`PublicSurface.approved.txt` moved in the same commit.

- **The state record is this node's exchange shape** (F-AR-02, option a, decided at
  the root). `StateRecord` keeps its five positional parameters and gains
  `AreaRatios`, `PressureRatios` and a nullable `Flow` as init properties, so that no
  construction site changes and its constructor stays within the parameter limit;
  `HasExits` is the one statement of the kind rule; `SolveRocketStates` solves the
  records with exits; `StateRecordException` carries `Index` and `Reason`, so that a
  caller renames the subject without re-deciding a rule. The command line's copies of
  the rules leave in its own design session.
- **`RocketSweep` is retired** (F-PR-06). The command line expands its sweeps itself,
  over a wider product (ratio, pressure and temperature, rocket and equilibrium), and
  the library's batch is the list of mixtures with the list of problems. The sweep
  overload built its system from the first case alone and bypassed the union path: a
  second implementation of one rule, with its own behaviour and one consumer, its own
  tests. The root `API.md`'s example follows in the same commit.
- **`Reactant.Custom` takes a `CustomReactantDefinition`** (F-PR-05): formula,
  enthalpy, temperature and the optional molar mass, the values that exist only
  together on a custom reactant, travel as one record, which `Reactant.Definition`
  exposes in place of the three nullable properties; the factory drops from eight
  parameters to five, and the two adjacent doubles can no longer be swapped silently.
- **`MixtureOf` and `CandidateSpeciesFor`** (F-PR-13): the two query methods read as
  `MassOf` does, and `Mixture` no longer collides with `Propellant.Mixture`.
- **`ElementalMixture.IsValidMassTolerance` is the one statement of the tolerance
  rule** (finite and non-negative; the review's open question 2): `Create` refuses
  with its message, the command line with its own, since it also refuses text that is
  no number.
- **The transport pass is narrowed, not the sentence** (F-PR-08): the runners group
  by the transport flag as they group by exit layout, so the contract's "a second
  pass over the stations of the cases that asked" holds.
- **A failed station's compositions are no solution**: they are computed from the
  moles the numerical nodes left for that station, the contract says so, and no test
  pins them (the review's open question 5).
- **Every public method of a disposed solver throws** (F-PR-09); the two properties
  stay readable. The fact is one theory over the public methods whose coverage is
  checked against the list reflection gives, so that a new method cannot be missed.
- **Size.** No type over 400 lines, no method over 60, no nesting deeper than 3, no
  more than 6 parameters. The published result records are the declared exception to
  the parameter rule (the root's code-shape constraint counts a record's constructor
  as a method): `RocketResult` (9), `Station` (8) and `EquilibriumResult` (8) are the
  contract's shape field for field, and the node constructs each in one place with
  named arguments.

  ⚠ 2026-09-14, found at this close, by the root's IL walk over parameters
  (`parameters.tsv`): two members over six parameters that were never declared an
  exception and are not one — a plain oversight of the internal decomposition, not a
  design decision. `RocketRunner.SolveGroup` and `EquilibriumRunner.SolveGroup` each
  took nine (`system, table, cases, members, wantsTransport, kinds`/`targets, masses,
  speciesNames, results`); what is fixed for the whole of one `Solve` call
  (`system, table, cases, masses, speciesNames, results`) is now a private
  `SolveContext` record struct built once, and each `SolveGroup` takes it plus the two
  or three arguments that vary per group (`members, wantsTransport` and `kinds` or
  `targets`) — four parameters. `ResolvedReactant` took eight; its enthalpy source
  (`HasFits`, `AssignedEnthalpy`, always set together by `ReactantResolver`'s two
  factory methods) is now init properties in the record's body, on `StateRecord`'s own
  precedent, leaving six in the primary constructor. Both of `ReactantResolver`'s
  construction sites updated to the object-initializer syntax the split needs; every
  other read of either type's fields is unchanged (`resolved[k].HasFits`,
  `r.AssignedEnthalpy` and the like read an init property exactly as they read a
  constructor-set one). Neither is a coupling question, so neither waits on the design
  session: `dotnet build` clean, the fast suite green unchanged (1111/1111 in this
  node), `Bits.approved.txt` and `PublicSurface.approved.txt` unmoved.

  ⚠ 2026-09-15: `HasFits` and `AssignedEnthalpy` are gone as stored properties (the
  clean-code repair's R-Problems-2). Both were exactly what `ReactantResolver`'s two
  factory methods could already derive from the constructor's own `Record` and
  `Reactant.Definition` — `Record is { Intervals.Count: > 0 }`, and
  `Record?.FormationEnthalpy ?? Reactant.Definition!.Enthalpy` — so storing them
  duplicated state instead of reading it once (a record with a body, not a
  parameter-count device: the primary constructor already counted six, without them,
  as above). They are now computed properties; both construction sites drop their
  object-initializer clause, and every read (`resolved[k].HasFits`,
  `r.AssignedEnthalpy`) is unchanged, a computed property read exactly as an init one.
  `dotnet test tests/Problems.Tests`: 1111/1111; `Bits.approved.txt` unmoved
  (26840f83).

- **The runners are the pipelines' composition roots** (added 2026-09-14 by the design
  session, after the close measured them). `RocketRunner` and `EquilibriumRunner` name
  both sides of the engine's boundary: the front door's cases, problems and records,
  and the execution node's batch, result and transport types, with the thermo,
  performance and transport structs a station carries; efferent coupling as the
  protocol tests node defines it measures 26 and 24. They hold no formula: the numbers
  they touch are copied into the batch or read back through `StationFactory`, the mass
  through `MixtureMass`, the checks through `ProblemValidation`; what they decide is the
  batching (by exit layout, by the transport flag) and the order of the engine runs.
  The root's exception covers a composition root that holds no formula, as it covers
  the execution node's four pipelines on the other side of the same boundary. A split
  into a batch filler and a result assembler was weighed and not taken: the assembler
  alone reads enough of the batch result to stay near the limit, for two more types
  and no rule made clearer. The close's note that the two "hold real rules, the kind
  the root's exception clause does not cover" read the clause as excluding any rule;
  it excludes a formula. The scratch reproduction of the walk lists 27 and 25: it
  counts `RocketResult[]` and `EquilibriumResult[]`, the result arrays `SolveContext`
  holds, apart from `RocketResult` and `EquilibriumResult`, while an array of a type of
  the tree adds no type of the tree.

  ⚠ 2026-09-15: `AtomicWeights`' row and its own summary read "the one translation of a
  missing atomic weight into an `ArgumentException` naming the element". Wrong from the
  type's introduction: `ReactantResolver.Custom` translates the same miss a second time,
  naming the reactant as well as the element (present already at 7661ea9,
  `Reactants.cs:354-362`, carried through every decomposition since). Unifying the two
  into one call site would change a message, which is out of scope here; the row and
  the type's summary now say what both translations do (the repair review's
  R-Problems-8). Found by the clean-code repair review.
- **The element order of first appearance is stated once** (2026-09-15, the clean-code
  repair's R-Problems-4). `Build`'s design named it one of the method's two duties
  (F-PR-03: "validated the mixture rule and derived the element order"), but e15d02f
  moved only the mixture rule out, to `MixtureRule.Validate`; the order itself stayed a
  nested loop inline in `Build` (nesting 3), and the same rule (BOOT.md, Constraints)
  was written a second time inside `ChemicalSystemCache.Union` (also nesting 3) for the
  union of several mixtures' elements. `ElementOrder.OfFirstAppearance(IEnumerable<IEnumerable<string>>)`
  states the rule once — every distinct symbol of a sequence of symbol lists, in the
  order first seen — and both call it: `Build` over each resolved reactant's formula
  symbols, `Union` over each mixture's own element list. Neither method's own loop
  survives: `Build` now nests 1, `Union` 2 (its own validation loop, unrelated to the
  element order, stays). Ce of `PropellantBuilder` and `ChemicalSystemCache` moves from
  10 to 11, both still under the root's limit of 14, no `## Shape exceptions` row
  needed. No behaviour change: `dotnet test tests/Problems.Tests`, 1111/1111;
  `Bits.approved.txt` unmoved (26840f83).

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Solver` | efferent coupling | 22 | the composition root of the front door: owns the engine and the collaborators, turns each public entry point into a system and cases and hands them to a runner; holds no rule |
| `RocketRunner` | efferent coupling | 26 | the composition root of the rocket pipeline: groups the cases, fills the batch, runs the engine and the transport pass, assembles the stations through `StationFactory`; holds no formula (the decision "The runners are the pipelines' composition roots") |
| `EquilibriumRunner` | efferent coupling | 24 | the composition root of the equilibrium pipeline, as `RocketRunner` |
| `RocketResult.RocketResult` | parameters | 9 | a published result record, the contract's shape field for field (the decision "Size"); created once, with named arguments |
| `Station.Station` | parameters | 8 | a published result record, as `RocketResult` |
| `EquilibriumResult.EquilibriumResult` | parameters | 8 | a published result record, as `RocketResult` |

## Acceptance criteria

- [x] 2026-09-12 — For the RP-1311 examples and the four reference propellants, the
      element moles per kilogram and the reactant enthalpy per kilogram computed here
      equal the reference's (the fixtures record the mass fractions, `elementMoles`
      and `reactantEnthalpy`) within 1e-10 relative:
      `PropellantTests.Element_moles_and_enthalpy_equal_the_reference_from_its_mass_fractions`
      over every rocket, tp, hp and sp file (the list from the directory listing, 195
      that day), the propellant given by the mass fractions the reference recorded.
      The ratio path (`A_ratio_split_reproduces_the_reference_mass_fractions_within_its_single_precision`)
      holds at 1e-7: the reference rounds the ratio to single precision before
      splitting the kilogram (Fixtures BOOT.md), so its own mass fractions carry that
      rounding; mole amounts: `Mole_amounts_are_converted_with_the_record_molar_mass`.
- [x] 2026-09-12 — The candidate species list for each fixture case equals the
      reference's product list under the same `Omit` list, or the `Only` list the
      reference was given (RP-1311 examples 1 and 12), compared as sets and by count:
      `PropellantTests.Candidate_species_equal_the_reference_product_list` over the
      same files; the order rule: `Candidates_are_gases_then_condensed_species_in_database_order`.
- [x] 2026-09-12 — A custom reactant (the AP/binder case's binder) produces the
      reference `b_i` and `h_0`: the AP/HTPB/Al files of the first criterion, and
      `A_custom_reactant_derives_its_molar_mass_from_the_formula_and_the_atomic_weights`.
- [x] 2026-09-12 — An `ElementalMixture` built from the `b_i` and `h_0` of a fixture
      propellant gives the same rocket and equilibrium results as the propellant itself,
      bit for bit on the same accelerator
      (`RocketTests.An_elemental_mixture_reproduces_its_propellant_bit_for_bit`); a
      state batch of the fixture stations reproduces the fixtures within the tolerance
      table, including records where an element of the batch is absent
      (`EquilibriumTests.State_batches_over_the_union_of_elements_reproduce_the_reference`;
      2026-09-13 for the batch over several mixtures:
      `RocketTests.Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`).
- [x] 2026-09-12 — End-to-end: every rocket fixture (the four reference propellants in
      shifting and frozen flow, with and without transport, and the RP-1311 rocket
      examples) and every tp, hp and sp fixture through this node match the fixtures
      within the tolerance table: `RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`
      and the three `EquilibriumTests` theories of the front door tests node, over the
      directory listings.
- [x] 2026-09-12 — A reactant temperature outside its record's range, an unknown
      reactant, a mixture with a zero-mass group, or an element without an atomic
      weight are rejected with the reactant's name in the exception, before any kernel
      runs: `RejectionTests` (`An_unknown_reactant_is_rejected_by_name`,
      `A_temperature_outside_the_record_range_is_rejected_by_name`,
      `Mixture_rules_that_leave_a_group_empty_or_ambiguous_are_rejected`,
      `A_custom_reactant_with_an_unknown_element_is_rejected_by_name`,
      `An_only_list_beyond_the_elements_is_rejected_and_a_valid_one_is_used_as_given`,
      `Invalid_state_records_are_rejected_by_index_or_element`,
      `Problems_without_the_data_they_need_are_rejected`, `A_disposed_solver_refuses_work`).
- [x] 2026-09-12 — Two identical batches produce identical results (statuses and
      numbers): `RocketTests.Identical_problems_give_identical_results_alone_and_in_one_call`,
      `A_sweep_equals_its_cases_solved_one_by_one`,
      `Problems_with_different_exit_layouts_are_solved_in_one_call_in_order`.
- [x] 2026-09-13 — A composition that does not weigh one kilogram is refused with
      its mass and the tolerance, and one that does is solved: the record of the
      invariant above passes as a state record; doubled, in mol/g or kmol/kg and in
      mmol/kg it is refused through `SolveStates` (`state record 0`), through
      `Solve(ElementalMixture, …)` for a rocket and for an equilibrium problem and
      through the batch over mixtures (`mixture 1`); the grams in the message equal
      `Σ n_i A_i` with the database's atomic weights; a record 0.9 % heavy solves and
      one 1.1 % heavy is refused
      (`RejectionTests.A_composition_that_does_not_weigh_one_kilogram_is_rejected_with_its_mass_and_the_tolerance`);
      the propellant path is covered by the committed file's `ADN` record
      (`A_reactant_record_whose_molar_mass_contradicts_its_formula_is_caught_at_the_solve`).
      Every fixture keeps passing through the end-to-end theories, unchanged (their
      element moles were measured within 1.65e-5 of one kilogram, see the invariant).
- [x] 2026-09-13 — The tolerance a mixture declares is the one the check applies,
      through every front door: the record of the invariant made 2 % heavy is refused
      at the default and solved at 3 %, through `StateBatchOptions.MassTolerance` for
      every record of a batch and through `Create` for the direct overloads; made 5 %
      heavy it is refused at 3 % with the message naming `3 %`; the propellant path
      and a mixture naming no tolerance declare the default; a negative, NaN or
      infinite tolerance is refused by `Create` naming `massTolerance`
      (`RejectionTests.The_tolerance_a_mixture_declares_is_the_one_applied`). Heavy,
      not light, for the reason recorded under the invariant.
- [x] 2026-09-13 — The mass is reported: `Solver.MassOf` equals `Σ n_i A_i` from
      `SpeciesDatabase.AtomicWeight`, and over every fixture file (the directory
      listing, 195 files) the recorded element moles lie within 1.7e-5 of one
      kilogram, the figure the derivation above rests on
      (`PropellantTests.The_recorded_element_moles_of_every_fixture_weigh_one_kilogram_within_the_derivation_figure`);
      every result's `MixtureMass` equals `MassOf` of its mixture on both front doors
      and through `SolveStates`, and a mixture made 0.5 % heavy reports 1.005, not one
      (`PropellantTests.Results_carry_the_mass_of_their_mixture`).
- [x] 2026-09-13 — A cut condensed record is one name in every result: for a mixture
      holding `ALN(L)` the stations' mole fractions and condensed mass fractions
      carry `ALN(L)` once with the sum of its pieces and `Species` lists it once
      (`SplitRecordTests.A_cut_species_reports_one_entry_under_its_database_name`);
      an hp state whose assigned enthalpy lies inside the record's 2700 K gap (the
      mass-tolerance invariant's record, made light) converges to the pinned pair at
      the crossing instead of `NotConverged`
      (`SplitRecordTests.An_enthalpy_inside_the_ALN_gap_solves_through_the_front_door`);
      and the sweep across the alumina plateau stays on the isentrope by either path
      (`SplitRecordTests.A_sweep_across_the_alumina_plateau_stays_on_the_isentrope_by_either_path`).
- [x] 2026-09-14 — The decomposition of `## Structure` (2026-09-14): every type within the
      root's code-shape constraint (`Solver` and the two runners the declared composition
      roots, their measured Ce written into the table); the tests node's front-door bit
      snapshot unchanged, recorded before any code moved; every fixture theory green
      unchanged; the surface moved only by the members `API.md` plans under 2026-09-14,
      in one contract commit after the internal moves, with `PublicSurface.approved.txt`
      moved in it; the command line's call sites adapted to the renames and to
      `CustomReactantDefinition`, nothing else of it touched. Reformulated 2026-09-14: it
      named `Solver` alone, the close measured the two runners over the limit, and the
      design session declared them (the decision "The runners are the pipelines'
      composition roots") rather than split them.

      Shape, measured 2026-09-14 on the build of `a3b7d05`, merged as `765f4e2`: efferent
      coupling by the dependency check's walk, `Solver` 22, `RocketRunner` 26 and
      `EquilibriumRunner` 24 (the rows of `## Shape exceptions`), every other type of the
      node 13 or below; no method or constructor declaring over six parameters outside
      the three records' rows, whose single creations name their arguments; lines and
      nesting by the close's reading until the protocol tests node's `ShapeTests` measures
      them (no file over 270 lines, the longest method `RocketRunner.SolveGroup` at 53,
      nesting at most 3, within the limit). The merge's fast suite green (3007 tests,
      `Problems.Tests` 1111, `Cli.Tests` 85).

      Bit snapshot: `git log --follow -- tests/Problems.Tests/Bits.approved.txt` names
      one commit, `8f8263c` itself — no commit since has touched the file — and the
      working tree carries no further diff against it either, checked repeatedly
      through this close. Fixture theories: every L0–L2 theory of the tests
      node green throughout the decomposition, 1111/1111 in `Problems.Tests` at the
      close (no fixture skipped, none removed). Surface: `PublicSurface.approved.txt`
      regenerated once, in the contract commit, its diff read in full against the
      contract's `API.md` before approving (net +6: `CustomReactantDefinition` and
      `StateRecordException` gained, `RocketSweep` dropped, `Reactant.Custom` and four
      `Solver` members changed signature); unmoved since. Command line: `git diff
      --stat 8f8263c..HEAD -- src/Cli` names only `src/Cli/Solving.cs`, the two
      authorized mechanical fixes (`MixtureOf`/`CandidateSpeciesFor` renames,
      `CustomReactantDefinition` construction), no rule of the command line changed.

      ⚠ 2026-09-15: this criterion's Shape paragraph read "nesting at most 2". Wrong
      already at the close: `ChemicalSystemCache.Union`, `Reactant.Reactant` and
      `PropellantBuilder.Build` each nest 3 (an `if`/`for` chain), within the root's
      limit of 3 but above what this paragraph claimed. Corrected to the true figure;
      found by the clean-code repair review (R-Problems-10). The same review found
      `RocketCase` and `EquilibriumCase` declared inside their runners' files, against
      this section's own "one type per file"; moved to `RocketCase.cs` and
      `EquilibriumCase.cs`, needing no further correction here.
- [x] 2026-09-14 — The state record with exits: a rocket record through
      `SolveRocketStates` equals the same mixture and problem through
      `Solve(mixtures, problems)` bit for bit, transport included
      (`RocketTests.A_state_record_with_exits_equals_its_case_through_the_batch_over_mixtures`);
      `SolveStates` refuses a record with exits and `SolveRocketStates` one without; a
      record with two targets, with exits and no enthalpy, or with a flow and no exits
      is refused; each refusal a `StateRecordException` whose `Index` is the record's
      and whose `Reason` names the rule
      (`RejectionTests.A_state_record_that_breaks_a_rule_of_its_shape_is_refused_with_its_index`,
      the `ShapeViolations` theory data, eight rows — correcting this line's citation
      of `EquilibriumTests`, which carries no fact of this criterion).
- [x] 2026-09-14 — The narrowed transport pass and the retired sweep: a batch of two
      rocket problems with transport on one of them gives, for each, the result of
      that problem solved alone bit for bit, transport figures included, and the
      other's `TransportStatus` null
      (`RocketTests.A_batch_mixing_transport_and_none_equals_each_problem_solved_alone`,
      with `Cases_are_grouped_by_exit_layout_and_transport_flag` over a four-case
      interleaved batch for the grouping itself); a ratio and pressure product given as
      a list of mixtures and problems equals its cases solved one by one bit for bit,
      the fact that replaced the sweep
      (`RocketTests.A_ratio_and_pressure_product_as_one_batch_equals_its_cases_solved_one_by_one`);
      every public method of a disposed solver throws, checked against the list of
      methods reflection gives so a new overload cannot be missed
      (`RejectionTests.Every_public_method_of_a_disposed_solver_throws` with
      `The_disposal_facts_cover_every_public_method_of_the_solver`).

## Taboos

- No numerical formula of the solvers here: this node computes only what a
  propellant definition implies (`b_i`, `h_0`, tables).
- No evaluation of a species polynomial here: a reactant record's enthalpy comes
  from the execution node's species-function batch, the one implementation.
- No file access: the database comes from the caller.
- No silent defaults for missing data: an unknown species or a missing enthalpy is an error.
- No unit other than SI in a public type; no seconds for specific impulse.
- No index-based composition in a result: names only.
