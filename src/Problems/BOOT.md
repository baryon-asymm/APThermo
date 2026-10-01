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
  `Solver.MassOf`, so that a raised tolerance never hides the figure. The default 1e-2
  is the lower end of the window, 1e-2 to 0.17, between what must pass (a record's
  rounding: every fixture file's element moles are within 1.7e-5 of one kilogram, the
  `HAN` record 4.5e-4; another atomic-weight table, below 1e-4; a trace element
  omitted, never one worth over a percent of the mass) and what must fail (a
  composition per kilogram of one reactant, 17 % off at the nearest; per pound, per two
  kilograms, per 100 g; in mol/g, kmol/kg or mmol/kg). No "warning" mode and no
  normalization of the moles are offered. The tests of the declared tolerance use a
  record made heavy, not light: made light it meets the `ALN(L)` gap. The propellant
  path is held to the same check: a reactant record whose molar mass contradicts its
  formula is refused (the committed `ADN`, 630.0 kg/kmol against its formula's 124.06).
  ⚠ 2026-09-13: was no check of the mass (a record of another simulation, C, H, O, N,
  Cl, Al, 1000.015 g, solved with every mole doubled), now refused beyond the tolerance
  → HISTORY.md#invariants-mass-tolerance
- **Candidate species are chosen by one rule**: every gaseous product species of the
  database whose elements are all among the mixture's elements, then every condensed
  product species under the same condition, each in database order, minus the `Omit`
  list, or exactly the `Only` list when given; ionized species (`E` in the formula,
  not a trailing sign in the name) and inert pseudo-element records are never
  candidates in version 1; a name with several records (a condensed species with one
  record per temperature range) is one candidate. An omitted name that is no product
  species is ignored, as the reference ignores it: the RP-1311 example 3 omit list
  names reactant-only species and old spellings.
  ⚠ 2026-09-12: was ions by a trailing sign in the name, now `E` in the formula
  → HISTORY.md#invariants-candidates-condensed
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

  ⚠ 2026-09-12: was a temperature outside the record's range an error, now accepted
  within the margin of 10 K → HISTORY.md#invariants-temperature-margin
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

Outside the tree: the .NET base class library; ILGPU (2026-09-15, distribution
phase), a direct `PackageReference` of this node's own project so that it is the
package's one real dependency once packed (`## Structure`, Packing) — this node's
code still names no ILGPU type itself.

⚠ 2026-09-12: was no `Equilibrium` in this list, now linked above
→ HISTORY.md#dependencies-equilibrium-link

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

  ⚠ 2026-09-12: was mole fractions of the gaseous phase, now over all species
  → HISTORY.md#constraints-mole-fractions

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

  ⚠ 2026-09-14: was one conversion besides mass normalization, now the three above
  → HISTORY.md#constraints-unit-conversions
- The mass tolerance is a declaration about the input, not a physical quantity: it
  travels with the mixture (2026-09-13), never with a problem, and the command line
  passes it as a run option, not as a field of a document.
- Element symbols are matched to the database spelling case-insensitively (`Al`,
  `al` and `AL` are the same element); the result reports the database spelling.
- Single-case calls are batches of one.
- The solver keeps the uploaded tables of every element set and species list it has
  seen, and the reactant enthalpies of every propellant instance, until it is disposed.

- **Audit fixes of 2026-09-26** (the hidden-defect audit of that day, Data, Problems and
  Cli, findings 3 to 8). Each rule below is a decision of the design session of that day:
  - **A refused state record is named by its own index.** Every refusal of a record by
    `SolveStates` or `SolveRocketStates` is a `StateRecordException` whose `Index` is
    the record's position in the list given. That covers the shape rules, the rules
    `ProblemValidation` applies (a non-positive or non-finite pressure, a tp
    temperature, an exit value, a non-finite target) and an element the database
    lacks.
    - ⚠ 2026-09-26: was a plain `ArgumentException` naming the batch-local problem,
      now a `StateRecordException` → HISTORY.md#audit-0926-record-index
  - **An element with no candidate species is refused.** The refusal is an
    `ArgumentException` naming the element and saying that only ionized or inert
    records carry it (`E`, `IH`, `IO` in the committed file). An element with no
    monatomic record to take its atomic weight from (`IC` of `InertRP-1`) is refused
    with a message that says exactly that, not "has no record in the database".
    - ⚠ 2026-09-26: was a row without species solved as `SingularMatrix`, now refused
      → HISTORY.md#audit-0926-no-candidates
  - **One amount kind per role group.** A group mixing mass-fraction and mole amounts
    is refused, naming the group and its reactants.
    - ⚠ 2026-09-26: was a mass fraction summed as grams with moles, now refused
      → HISTORY.md#audit-0926-amount-kinds
  - **A reactant name with several records.**
    - Its temperature range is the union of its records' ranges, which Thermo joins
      into one table species.
    - Several reactant-only records of one name resolve to the last, as cea 3.3.4
      does (`n-Butanol`: gas then liquid, resolves to the liquid).
    - ⚠ 2026-09-26: was the first record taken for both, now the union of the ranges
      and the last of the reactant-only records → HISTORY.md#audit-0926-multi-record
  - **A failed station's transport status is null.** `TransportStatus` is null when
    transport was not requested or when the station did not converge. The station's
    own status tells the two apart.
    - ⚠ 2026-09-26: was "null when transport was not requested" in the contract, now
      also null on a failed station → HISTORY.md#audit-0926-transport-status

- **Audit fixes of 2026-09-28** (the second hidden-defect audit, Data, Problems and Cli,
  findings F1 to F3 and observations 1 to 7). They refine three rules of 2026-09-26
  above, whose sentences stay as they were decided, and add five:
  - **An element without candidates is refused only when it carries moles.** An
    element with no candidate species is refused only when some mixture of the batch
    gives it a nonzero abundance. An element at zero in every mixture is masked, as the
    equilibrium node masks any absent element, and the result is the result without
    it.
    - ⚠ 2026-09-28: was an element at zero abundance refused with the rest, now masked
      → HISTORY.md#audit-0928-zero-abundance
  - **The refusal names its real cause.** When the candidates of an element were
    removed by an `Only` or `Omit` list, the refusal says so, and names the list. The
    wording "only ionized or inert records carry it" is kept for the database's own
    case (observation 1). → HISTORY.md#audit-0928-refusal-cause-example
  - **An `Only` list names candidates only.** A name in `Only` that is an ionized
    species or an inert pseudo-element record is refused by name, as the candidate
    rule above excludes them (observation 2). → HISTORY.md#audit-0928-only-example
  - **One amount kind per unit of normalization.** The kinds are checked where the
    kilogram is split. With a ratio, that is per role group, as before. Without a
    ratio, the whole propellant is one kilogram, so every reactant of every group
    shares one kind.
    - ⚠ 2026-09-28: was the kinds checked per group alone, now across the groups when
      no ratio splits the kilogram → HISTORY.md#audit-0928-amount-kinds
  - **A record is named for its own elements; a batch is named for its options.**
    `SolveStates` and `SolveRocketStates` check each record's own elements (atomic
    weight, candidates) before the union of the batch is built. A record that fails is
    a `StateRecordException` with its index. A condition of the batch, not of a record,
    is an `ArgumentException` naming the option: transport requested from a database
    loaded without `trans.inp`, an invalid `StateBatchOptions.MassTolerance`.
    - ⚠ 2026-09-28: was an element the database lacks found on the union and a batch
      condition blamed on record 0, now each named for its own cause
      → HISTORY.md#audit-0928-record-or-batch
  - **Estimates are finite when given.** A rocket's `TemperatureEstimate` or an
    equilibrium problem's `Temperature` of ±∞ is refused, as NaN and negative values
    are (observation 5: +∞ passed and ended `SingularMatrix` at the chamber).
  - **Amounts sum to a finite value.** A group whose amounts sum to a non-finite
    value is refused, naming the group (observation 7).
    → HISTORY.md#audit-0928-overflow-example
  - **Reactant records resolve by the rules that name them.**
    - The last record is taken only for a reactant-only name. A product name with
      several records keeps the union of ranges, and its enthalpy comes from the joined
      table species.
    - A name whose records mix fitted and unfitted ones is refused, since no committed
      name does and the reference's behaviour for it is not established
      (observation 3).
    - A reactant's temperature window uses the lowest lower and the highest upper bound
      of its records, the thermo node's `RecordLow`/`RecordHigh` rule. Observation 6:
      `Br2(cr)`, written 300 → 265.9 K, had an empty window and could not be used at
      any temperature; cea 3.3.4 evaluates it at 298.15 K.
  - **The chemical-system cache key is unambiguous.** It no longer joins names with a
    comma, which 84 product names contain (observation 4): `omit` lists
    `["B2H4,db"]` and `["B2H4", "db"]` built the same key.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The data
flow is `Solver` → `PropellantMixtures` (the propellant front door only) →
`ChemicalSystemCache` → `ProblemValidation` and `MixtureMass` per case →
`RocketRunner` or `EquilibriumRunner` → the engine → `StationFactory` → the result
records. Every type below is internal except where marked; one type per file, named
after the type; the public records keep their theme files.
→ HISTORY.md#structure-intro-condensed

⚠ 2026-09-15: was `MixtureSpecification` and `Propellant.Mixture` public and the three
results positionally constructible, now internal and nominal with an internal
constructor → HISTORY.md#structure-api-review

| Type | Responsibility | Visibility |
|---|---|---|
| `Solver` | the composition root: owns the engine and the collaborators below, turns each public entry point into (system, cases) and hands them to a runner; holds no rule. The declared exception to the coupling limit, its figure under `## Shape exceptions`; the reason is what it names, not a superlative: the two runners are declared composition roots as well (the decision "The runners are the pipelines' composition roots") → HISTORY.md#structure-solver-row | public |
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

⚠ 2026-09-15: was the `Solver` row ending in the runners' Ce 27 and 25, now a
cross-reference to the decision → HISTORY.md#structure-solver-row-stale

Decisions taken with the review of 2026-09-14, coded in the contract commit; `API.md`
carries the contracts. → HISTORY.md#structure-decisions-intro

- **The state record is this node's exchange shape** (F-AR-02, option a, decided at
  the root). `StateRecord` keeps its five positional parameters and gains
  `AreaRatios`, `PressureRatios` and a nullable `Flow` as init properties; `HasExits`
  is the one statement of the kind rule; `SolveRocketStates` solves the records with
  exits; `StateRecordException` carries `Index` and `Reason`, so that a caller renames
  the subject without re-deciding a rule.
- **`RocketSweep` is retired** (F-PR-06): the command line expands its sweeps itself
  and the library's batch is the list of mixtures with the list of problems; the
  sweep overload built its system from the first case alone and bypassed the union
  path, a second implementation of one rule. The root `API.md`'s example follows.
- **`Reactant.Custom` takes a `CustomReactantDefinition`** (F-PR-05): formula,
  enthalpy, temperature and the optional molar mass, which exist only together, travel
  as one record, which `Reactant.Definition` exposes; the factory drops from eight
  parameters to five. → HISTORY.md#structure-decisions-condensed
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

  ⚠ 2026-09-15: was `SolveGroup` at nine parameters in both runners (`SolveContext` of
  2026-09-14), `ResolvedReactant` at eight, now one `AdmittedCase` record struct per
  case built once in `Solve`; a `SolveGroup` takes `system`, its own
  `IReadOnlyList<AdmittedCase>`, `wantsTransport`, `speciesNames` (and `kinds`, in
  `RocketRunner` only) and returns its group's results, which `Solve` places back at
  their indices; `HasFits` and `AssignedEnthalpy` are computed from `Record` and
  `Reactant.Definition`, not stored → HISTORY.md#structure-solvegroup-shape

- **The runners are the pipelines' composition roots** (added 2026-09-14 by the design
  session). `RocketRunner` and `EquilibriumRunner` name both sides of the engine's
  boundary: the front door's cases, problems and records, and the execution node's
  batch, result and transport types, with the thermo, performance and transport
  structs a station carries; efferent coupling as the protocol tests node defines it
  measures 26 and 24. They hold no formula: the numbers they touch are copied into the
  batch or read back through `StationFactory`, the mass through `MixtureMass`, the
  checks through `ProblemValidation`; what they decide is the batching (by exit
  layout, by the transport flag) and the order of the engine runs. The root's
  exception covers a composition root that holds no formula, as it covers the
  execution node's four pipelines on the other side of the same boundary. A split
  into a batch filler and a result assembler was weighed and not taken: the assembler
  alone reads enough of the batch result to stay near the limit, for two more types
  and no rule made clearer. → HISTORY.md#structure-runners-condensed

  ⚠ 2026-09-15: was the scratch walk's 27 and 25 explained by `SolveContext`'s result
  arrays, now gone with it; the measured 26 and 24 stand
  → HISTORY.md#structure-runners-coupling-note

⚠ 2026-09-15: was `AtomicWeights` the one translation of a missing atomic weight, now
two, `ReactantResolver.Custom` naming the reactant too
→ HISTORY.md#structure-atomic-weights-two

- **The element order of first appearance is stated once** (2026-09-15, the clean-code
  repair's R-Problems-4):
  `ElementOrder.OfFirstAppearance(IEnumerable<IEnumerable<string>>)` holds the rule,
  every distinct symbol of a sequence of symbol lists in the order first seen, and
  both `PropellantBuilder.Build` (over each resolved reactant's formula symbols) and
  `ChemicalSystemCache.Union` (over each mixture's element list) call it; `Build`
  nests 1, `Union` 2, and the Ce of both is 11. → HISTORY.md#structure-element-order

**Packing (2026-09-15, distribution phase, root `BOOT.md`, `## Delivery`, Packages).**
This node's project is the one packed as `APThermo` (`IsPackable=true`,
`PackageId=APThermo`; version and metadata from the root's `Directory.Build.targets`)
and carries the assemblies of the six nodes it depends on, never released apart from
it: every `ProjectReference` to them is `PrivateAssets="all"`; a direct
`PackageReference` to `ILGPU` keeps it the package's one real nuspec dependency; two
targets (`TargetsForTfmSpecificBuildOutput`,
`TargetsForTfmSpecificDebugSymbolsInPackage`, both
`DependsOnTargets="ResolveReferences"`) add the six `.dll` to `lib/net10.0` and their
`.pdb`, derived from the resolved `.dll` paths, to the `.snupkg`;
`docs/nuget/APThermo.md` and `data/NOTICE` are packed as `None` items. Evidence: the
criterion of 2026-09-15 below.
→ HISTORY.md#structure-packing-condensed

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Solver` | efferent coupling | 24 | the composition root of the front door: owns the engine and the collaborators, turns each public entry point into a system and cases and hands them to a runner; holds no rule |
| `RocketRunner` | efferent coupling | 26 | the composition root of the rocket pipeline: groups the cases, fills the batch, runs the engine and the transport pass, assembles the stations through `StationFactory`; holds no formula (the decision "The runners are the pipelines' composition roots") |
| `EquilibriumRunner` | efferent coupling | 24 | the composition root of the equilibrium pipeline, as `RocketRunner` |
| `RocketResult.RocketResult` | parameters | 9 | a published result record, the contract's shape field for field (the decision "Size"); created once, with named arguments |
| `Station.Station` | parameters | 8 | a published result record, as `RocketResult` |
| `EquilibriumResult.EquilibriumResult` | parameters | 8 | a published result record, as `RocketResult` |

⚠ 2026-09-28: was `Solver`'s Ce 22, now 24 (`ValidatedOptions` and
`ValidateRecordElements`, composition-root code, no rule)
→ HISTORY.md#shape-solver-ce-24

## Acceptance criteria

- [x] 2026-09-12 — For the RP-1311 examples and the four reference propellants, the
      element moles per kilogram and the reactant enthalpy per kilogram computed here
      equal the reference's (the fixtures record the mass fractions, `elementMoles`
      and `reactantEnthalpy`) within 1e-10 relative:
      `PropellantTests.ElementMolesAndEnthalpyEqualTheReferenceFromItsMassFractions`
      over every rocket, tp, hp and sp file (the list from the directory listing, 195
      that day), the propellant given by the mass fractions the reference recorded.
      The ratio path (`ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision`)
      holds at 1e-7: the reference rounds the ratio to single precision before
      splitting the kilogram (Fixtures BOOT.md), so its own mass fractions carry that
      rounding; mole amounts: `MoleAmountsAreConvertedWithTheRecordMolarMass`.
- [x] 2026-09-12 — The candidate species list for each fixture case equals the
      reference's product list under the same `Omit` list, or the `Only` list the
      reference was given (RP-1311 examples 1 and 12), compared as sets and by count:
      `PropellantTests.CandidateSpeciesEqualTheReferenceProductList` over the
      same files; the order rule: `CandidatesAreGasesThenCondensedSpeciesInDatabaseOrder`.
- [x] 2026-09-12 — A custom reactant (the AP/binder case's binder) produces the
      reference `b_i` and `h_0`: the AP/HTPB/Al files of the first criterion, and
      `ACustomReactantDerivesItsMolarMassFromTheFormulaAndTheAtomicWeights`.
- [x] 2026-09-12 — An `ElementalMixture` built from the `b_i` and `h_0` of a fixture
      propellant gives the same rocket and equilibrium results as the propellant itself,
      bit for bit on the same accelerator
      (`RocketTests.AnElementalMixtureReproducesItsPropellantBitForBit`); a
      state batch of the fixture stations reproduces the fixtures within the tolerance
      table, including records where an element of the batch is absent
      (`EquilibriumTests.StateBatchesOverTheUnionOfElementsReproduceTheReference`;
      2026-09-13 for the batch over several mixtures:
      `RocketTests.Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`).
- [x] 2026-09-12 — End-to-end: every rocket fixture (the four reference propellants in
      shifting and frozen flow, with and without transport, and the RP-1311 rocket
      examples) and every tp, hp and sp fixture through this node match the fixtures
      within the tolerance table: `RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
      and the three `EquilibriumTests` theories of the front door tests node, over the
      directory listings.
- [x] 2026-09-12 — A reactant temperature outside its record's range, an unknown
      reactant, a mixture with a zero-mass group, or an element without an atomic
      weight are rejected with the reactant's name in the exception, before any kernel
      runs: `RejectionTests` (`AnUnknownReactantIsRejectedByName`,
      `ATemperatureOutsideTheRecordRangeIsRejectedByName`,
      `MixtureRulesThatLeaveAGroupEmptyOrAmbiguousAreRejected`,
      `ACustomReactantWithAnUnknownElementIsRejectedByName`,
      `AnOnlyListBeyondTheElementsIsRejectedAndAValidOneIsUsedAsGiven`,
      `InvalidStateRecordsAreRejectedByIndexOrElement`,
      `ProblemsWithoutTheDataTheyNeedAreRejected`, `ADisposedSolverRefusesWork`).
- [x] 2026-09-12 — Two identical batches produce identical results (statuses and
      numbers): `RocketTests.IdenticalProblemsGiveIdenticalResultsAloneAndInOneCall`,
      `A_sweep_equals_its_cases_solved_one_by_one`,
      `ProblemsWithDifferentExitLayoutsAreSolvedInOneCallInOrder`.
- [x] 2026-09-13 — A composition that does not weigh one kilogram is refused with
      its mass and the tolerance, and one that does is solved: the record of the
      invariant above passes as a state record; doubled, in mol/g or kmol/kg and in
      mmol/kg it is refused through `SolveStates` (`state record 0`), through
      `Solve(ElementalMixture, …)` for a rocket and for an equilibrium problem and
      through the batch over mixtures (`mixture 1`); the grams in the message equal
      `Σ n_i A_i` with the database's atomic weights; a record 0.9 % heavy solves and
      one 1.1 % heavy is refused
      (`RejectionTests.ACompositionThatDoesNotWeighOneKilogramIsRejectedWithItsMassAndTheTolerance`);
      the propellant path is covered by the committed file's `ADN` record
      (`AReactantRecordWhoseMolarMassContradictsItsFormulaIsCaughtAtTheSolve`).
      Every fixture keeps passing through the end-to-end theories, unchanged (their
      element moles were measured within 1.65e-5 of one kilogram, see the invariant).
- [x] 2026-09-13 — The tolerance a mixture declares is the one the check applies,
      through every front door: the record of the invariant made 2 % heavy is refused
      at the default and solved at 3 %, through `StateBatchOptions.MassTolerance` for
      every record of a batch and through `Create` for the direct overloads; made 5 %
      heavy it is refused at 3 % with the message naming `3 %`; the propellant path
      and a mixture naming no tolerance declare the default; a negative, NaN or
      infinite tolerance is refused by `Create` naming `massTolerance`
      (`RejectionTests.TheToleranceAMixtureDeclaresIsTheOneApplied`). Heavy,
      not light, for the reason recorded under the invariant.
- [x] 2026-09-13 — The mass is reported: `Solver.MassOf` equals `Σ n_i A_i` from
      `SpeciesDatabase.AtomicWeight`, and over every fixture file (the directory
      listing, 195 files) the recorded element moles lie within 1.7e-5 of one
      kilogram, the figure the derivation above rests on
      (`PropellantTests.TheRecordedElementMolesOfEveryFixtureWeighOneKilogramWithinTheDerivationFigure`);
      every result's `MixtureMass` equals `MassOf` of its mixture on both front doors
      and through `SolveStates`, and a mixture made 0.5 % heavy reports 1.005, not one
      (`PropellantTests.ResultsCarryTheMassOfTheirMixture`).
- [x] 2026-09-13 — A cut condensed record is one name in every result: for a mixture
      holding `ALN(L)` the stations' mole fractions and condensed mass fractions
      carry `ALN(L)` once with the sum of its pieces and `Species` lists it once
      (`SplitRecordTests.ACutSpeciesReportsOneEntryUnderItsDatabaseName`);
      an hp state whose assigned enthalpy lies inside the record's 2700 K gap (the
      mass-tolerance invariant's record, made light) converges to the pinned pair at
      the crossing instead of `NotConverged`
      (`SplitRecordTests.AnEnthalpyInsideTheALNGapSolvesThroughTheFrontDoor`);
      and the sweep across the alumina plateau stays on the isentrope by either path
      (`SplitRecordTests.ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`).
- [x] 2026-09-14 — The decomposition of `## Structure` (2026-09-14): every type within
      the root's code-shape constraint (`Solver` and the two runners the declared
      composition roots, their Ce in `## Shape exceptions`); the tests node's front-door
      bit snapshot unchanged, recorded before any code moved; every fixture theory green
      unchanged; the surface moved only by the members `API.md` plans under 2026-09-14,
      in one contract commit after the internal moves, with `PublicSurface.approved.txt`
      moved in it; the command line's call sites adapted to the renames and to
      `CustomReactantDefinition`, nothing else of it touched. Shape measured on the
      build of `a3b7d05`, merged as `765f4e2`, the fast suite green (3007 tests,
      `Problems.Tests` 1111). → HISTORY.md#criterion-decomposition-2026-09-14
- [x] 2026-09-14 — The state record with exits: a rocket record through
      `SolveRocketStates` equals the same mixture and problem through
      `Solve(mixtures, problems)` bit for bit, transport included
      (`RocketTests.AStateRecordWithExitsEqualsItsCaseThroughTheBatchOverMixtures`);
      `SolveStates` refuses a record with exits and `SolveRocketStates` one without; a
      record with two targets, with exits and no enthalpy, or with a flow and no exits
      is refused; each refusal a `StateRecordException` whose `Index` is the record's
      and whose `Reason` names the rule
      (`RejectionTests.AStateRecordThatBreaksARuleOfItsShapeIsRefusedWithItsIndex`,
      the `ShapeViolations` theory data, eight rows — correcting this line's citation
      of `EquilibriumTests`, which carries no fact of this criterion).
- [x] 2026-09-14 — The narrowed transport pass and the retired sweep: a batch of two
      rocket problems with transport on one of them gives, for each, the result of
      that problem solved alone bit for bit, transport figures included, and the
      other's `TransportStatus` null
      (`RocketTests.ABatchMixingTransportAndNoneEqualsEachProblemSolvedAlone`,
      with `CasesAreGroupedByExitLayoutAndTransportFlag` over a four-case
      interleaved batch for the grouping itself); a ratio and pressure product given as
      a list of mixtures and problems equals its cases solved one by one bit for bit,
      the fact that replaced the sweep
      (`RocketTests.ARatioAndPressureProductAsOneBatchEqualsItsCasesSolvedOneByOne`);
      every public method of a disposed solver throws, checked against the list of
      methods reflection gives so a new overload cannot be missed
      (`RejectionTests.EveryPublicMethodOfADisposedSolverThrows` with
      `TheDisposalFactsCoverEveryPublicMethodOfTheSolver`).
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 — Packing (`## Structure`, Packing): `dotnet pack
      APThermo.sln -c Release -o <feed>` from a clean build produces exactly
      `APThermo.0.1.0.nupkg`/`.snupkg` and `APThermo.Cli.0.1.0.nupkg`/`.snupkg`, no
      warning; the nupkg holds the seven merged assemblies in `lib/net10.0`, `README.md`,
      `NOTICE`, and a nuspec dependency list of exactly `ILGPU`; the snupkg the matching
      seven `.pdb`. `dotnet tool install APThermo.Cli --tool-path <dir> --add-source
      <feed> --version 0.1.0` then `apthermo --version` prints `0.1.0`, and running an
      approved Cli-node example (`rocket documents/rocket-lox-lh2.json --format csv
      --accelerator cpu`, no `--database`) from an empty directory reproduces
      `tests/Cli.Tests/documents/rocket-lox-lh2.approved.csv` byte for byte (the CSV
      form carries no database path field to differ). A throwaway console project
      referencing `APThermo` 0.1.0 from the feed solves the LOX/LH2 rocket case through
      `Solver` on the CPU accelerator and prints the chamber temperature the library
      gives directly (3485.023295679567 K). Verified by hand (packing is not part of
      `dotnet test`), not by a committed test.
      → HISTORY.md#criterion-packing-2026-09-15

- [x] 2026-09-26 — The audit fixes of that date (Constraints), each fact red once
      against the code of `9c33398` and green after (ticked 2026-09-27), in
      `tests/Problems.Tests/AuditFixTests.cs`:
      - `ARuleProblemValidationAppliesIsRefusedByTheRecordsOwnIndexNotABatchLocalOne`:
        a batch of three records with the third's pressure 0 is refused with `Index` 2;
      - `AnElementThatSurvivesOnlyInIonizedOrInertRecordsIsRefusedByName` and
        `AnElementWithNoMonatomicRecordIsRefusedNamingWhatIsMissing`: `InertH2(L)` and a
        record with `E` refused naming the element, `InertRP-1` naming `IC` and the
        missing monatomic record;
      - `MixedAmountKindsInOneRoleGroupAreRejected`: a fuel group of one mass-fraction
        and one mole reactant is refused;
      - `AMultiRecordProductNamesRangeIsTheUnionOfItsRecordsAndItsEnthalpyEqualsTheJoinedTable`:
        `Fe2O3(cr)` accepted at 1000 K, its enthalpy equal to the joined table species'
        (through `SolverFixture.Shared.Engine`, not a typed value);
      - `SeveralReactantOnlyRecordsOfOneNameResolveToTheLast`: `n-Butanol`'s enthalpy
        per kilogram equal to the database record's own field;
      - a failed station with transport requested has a null `TransportStatus`, which
        `RocketTests.AFailingStationIsAStatusAndNotAnException` already pinned.
      `dotnet test tests/Problems.Tests`: 1117 of 1117, none skipped; the public surface
      and every bit snapshot unmoved. → HISTORY.md#criterion-audit-0926

      ⚠ 2026-09-28: was three of these facts trying only one side of their rule, now the
      second audit's facts below → HISTORY.md#criterion-audit-0926-one-sided
- [x] 2026-09-28 — The audit fixes of 2026-09-28 (Constraints). Each fact is red once
      against `5a732f0`, then green, in `tests/Problems.Tests/SecondAuditFixTests.cs`:
      - `AnElementAtZeroAbundanceEverywhereIsMaskedNotRefused`: `"E": 0.0` beside the
        approved AP/Al record solves bit for bit as without `E`, `"e": 1e-6` is refused;
      - `AnOnlyListNamesItsOwnCauseAndExcludesIonsAndInertRecords`:
        `"only": ["H2", "H"]` on LOX/LH2 refused naming the list, `e-`, `H+` and
        `InertH` refused by name;
      - `OneAmountKindPerUnitOfNormalizationAppliesAcrossGroupsOnlyWithoutARatio`:
        refused across groups without a ratio, unchanged with one;
      - `ARecordIsNamedForItsOwnElementsABatchForItsOptions`: each bad record of a batch
        a `StateRecordException` with its own index, a transport request against a
        database without `trans.inp` and a NaN `StateBatchOptions.MassTolerance` an
        `ArgumentException` naming the option;
      - `EstimatesMustBeFinite` (+∞ refused), `AmountsMustSumToAFiniteValue` (two fuels
        of 1e308 refused naming the group);
      - `Br2ResolvesAt298Point15KAgainstTheGeneratedReference`: enthalpy equal to cea
        3.3.4's (`tests/Fixtures/cases/reactant/Br2_cr__298.15K.json`, never typed);
      - `TwoOmitListsThatJoinToTheSameTextGiveTwoTables`.
      `API.md`'s errors table states every refusal above. `dotnet test
      tests/Problems.Tests/APThermo.Problems.Tests.csproj`: 1206 of 1206, none skipped;
      no `Bits*.approved.txt` or `PublicSurface.approved.txt` moved.
      → HISTORY.md#criterion-audit-0928
- [x] 2026-09-28 — The third audit pass of 2026-09-28 (part 2, finding 3) is closed: the
      refusal of an element with no candidate species does not depend on what the solver
      solved before. `ChemicalSystemCache.Get(IReadOnlyList<string>, …)` calls
      `ValidatedCandidates` before the cache's `_systems.TryGetValue` lookup, so every
      call validates its own mixture's abundances; `Union` and the single-mixture
      `Solve` overloads go through the same `Get`. Fact:
      `ThirdPassFixTests.TheNoCandidateRefusalDoesNotDependOnTheCachesHistory`, red at
      `c02e14d`, green after. `dotnet test tests/Problems.Tests`: 1252 of 1252; the
      protocol lint 0 and 0; no `Bits*.approved.txt` or `PublicSurface.approved.txt`
      moved. → HISTORY.md#criterion-third-pass-0928

## Taboos

- No numerical formula of the solvers here: this node computes only what a
  propellant definition implies (`b_i`, `h_0`, tables).
- No evaluation of a species polynomial here: a reactant record's enthalpy comes
  from the execution node's species-function batch, the one implementation.
- No file access: the database comes from the caller.
- No silent defaults for missing data: an unknown species or a missing enthalpy is an error.
- No unit other than SI in a public type; no seconds for specific impulse.
- No index-based composition in a result: names only.
