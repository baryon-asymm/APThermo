# BOOT.md — Transport

## Purpose

Viscosity, thermal conductivity and Prandtl number of the mixture at one station of
one case, in the frozen and in the reacting (equilibrium) sense, from the NASA
transport fits and a composition. It is a separate node because it uses a different
data file, is an optional stage of every problem, and, by the root's decision, is
brought to the GPU after the thermodynamic path.

⚠ Declared deviation (`AGENTS.md` §6, §12): the method is that of NASA RP-1311 Part I,
chapter 5 (the pure-species fits, the mixture rules for viscosity and frozen
conductivity, the reaction contribution to the conductivity of a reacting mixture), in
the form the reference program applies it. This document fixes the choices, the units
and the rules of the reference that the report does not write down; it does not
restate the formulas of the report. Whoever codes this node reads chapter 5 and the
Constraints below.

## Invariants

- **Units are converted once, at the table build**: the fits give micropoise and
  μW/(cm·K); the factors to Pa·s (`1e-7`) and W/(m·K) (`1e-4`) are folded into the
  constant term of every fit, and the kernel code sees SI only.
- **Every gaseous species of the transport set takes part**: a species with a
  transport entry through its fits, a species without one through the reference's
  estimate (Constraints), which is counted and reported, never silent. Condensed
  species never take part.
- **Pair data first.** For a pair with an interaction entry the entry is used; without
  one, the estimate from the pure viscosities (the form of Wilke, equation 5.5). No
  third source.
- **Frozen and reacting values are consistent**: the reaction term is a quadratic form
  with a positive definite matrix, so the reacting conductivity is never below the
  frozen one, and the two are equal where the set has no reaction.
- **A larger table changes nothing.** A case evaluated in a table that holds its
  species in their order and, besides, the species of elements the case lacks gives
  the figures of its own table bit for bit: the set's thresholds count the gases of
  the case (those whose every element is active), the seeding and the passes skip
  zero-mole species, and the sums run over the set in table order. This is what lets
  the front door batch cases with different elements over one table.

  ⚠ 2026-09-13: was the set's thresholds counting the table's gaseous species, now
  the case's (a larger table moves no bit) → HISTORY.md#set-gas-count
- **Stateless, deterministic, no allocation**, as every numerical node.

⚠ 2026-09-12: was only species with data take part, the rest excluded with
`NoTransportData`, now every gaseous species takes part → HISTORY.md#species-no-data

## Dependencies

- [Data](../Data/API.md) — the transport fits and pair entries (`TransportDatabase`).
- [Thermo](../Thermo/API.md) — the species table view (molar masses, stoichiometry,
  the gaseous count), the species functions `Cp°/R` and `H°/RT`, `CaseStatus`, the
  gas constant.
- [Equilibrium](../Equilibrium/API.md) — the dense solver for the linear systems of
  the reaction terms, and the mole numbers of a station as its result reports them.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I, chapter 5; the
source of nasa/cea v3.3.4 (`source/equilibrium.f90`, subroutines
`compute_transport_properties` and `EqSolver_update_transport_basis`) as the record of
the rules below.

⚠ 2026-09-12: was the reaction term from Equilibrium's multipliers and derivatives,
now from the set's stoichiometry and enthalpies → HISTORY.md#reaction-inputs

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition, the rules of the
reference, recovered from its source:

- Kernel-compatible C#: a host-side table builder over `Data` for the species of a
  `SpeciesTable`, and a kernel-side evaluation of one station.
- The transport table stores, per species of the species table, the runs of viscosity
  and conductivity fits (zero fits for a species without an entry and for a condensed
  species) and, per pair of gaseous species with an interaction entry, its viscosity
  run, reachable through a dense pair index over the species table.
- Fit selection: the reference's rule, the last fit whose predecessor's upper bound
  lies below T, else the first: inside a fit that fit, on a bound shared by two fits
  the lower one, outside the runs the nearest fit extrapolates.
- The transport set (the reference's NM, at most 40 species): seeded with one
  component species per active element, chosen as the reference does (gaseous species
  in decreasing moles, each given the first element row it contains that is still free,
  provided its stoichiometry column is not that of an earlier component and stays
  independent of the rows' default species, the monatomic gases). The components are
  then settled before anything is seeded (2026-09-28): the element rows are reduced
  over the columns of the components and of the defaults, row by row in element order,
  and a row whose component's pivot has vanished, because its column was proportional
  to an earlier component's, takes back its default species; the row is skipped only
  when that pivot is zero too (cea 3.3.4 `equilibrium.f90:2264-2291`, which reverts
  inside its basis update and stores the reverted list). The set is seeded with the
  settled components, whatever their moles, a zero-mole default included (cea 3.3.4
  `equilibrium.f90:5221-5236`, where only "already selected" skips a component). Then every gaseous
  species with moles not below n/(ng·10^k), k = 1, 2, …, until the set carries
  (1 − 1e-9)(1 − 1e-6) of the gaseous moles n, the set is full, the threshold falls
  below 1e-11 n, or ng passes have run (the reference's bound, cea 3.3.4
  `equilibrium.f90:5278`) (ng is the number of gaseous species of the case: those of
  the table whose every element the case holds, which in a table built for the case
  alone is the table's gas count, the reference's product list). Within a pass the
  species are taken in table order, which matters only when the set fills: the
  AP/HTPB/Al chamber needs 56 species for the coverage and takes the first 40. Mole
  fractions x_s are relative to the set. → HISTORY.md#set-selection-wording

  ⚠ 2026-09-26: was a component kept when its pivot had vanished, now its row reverts
  to the default species → HISTORY.md#reduction-revert

  ⚠ 2026-09-28: was the revert in the reduction, after the seeding, now the components
  are settled before it → HISTORY.md#components-settled
- Species without data: η_i = (5/16)·sqrt(k_B M_i T/(π N_A))/(σ₀² Ω_i) with
  σ₀ = 1 Å and Ω_i = max(1, ln(50 M_i^4.6/T^1.4)), and
  λ_i = η_i (R/M_i)(3.75 + 1.32 (Cp_i/R − 2.5)). A species with viscosity fits but
  no conductivity fit (`UF6`) takes λ_i from that relation with its fitted η_i. The
  constants of these estimates are the model's and are written in the solver once;
  the reference writes them in its own units (26.7 μP·(kg/kmol·K)^−½, 0.00375 and
  0.00132 with η in μP and R in J/(kmol·K)); k_B and N_A carry the reference's values.
- Pairs without data: η_ij = 4√2 η_i sqrt(M_j/(M_i+M_j)) / (1 + sqrt((M_j/M_i)^½ η_i/η_j))²,
  equation (5.5) rewritten as an interaction viscosity. The reference writes 5.656854
  for 4√2; the 3e-8 the fixtures show against the tree is that rounding.
- Mixing: equations (5.3) and (5.4) with φ_ij = 2 M_j η_i/(η_ij (M_i+M_j)) (5.7) for
  every pair, data or estimate, and ψ_ij from φ_ij by (5.6).
- Reactions: the components are those of the seeding, settled before it (above), so
  every component has its column in the set; the stoichiometry of the set is
  reduced so that the component columns are unit vectors (always; the reference reduces
  only when a component differs from the row's monatomic default, which is the same
  matrix in every case with the monatomic gases present); one reaction forms each
  non-component species of the set from the components. A species of the set below
  1e-10 in x_s is eliminated from every reaction through the first reaction that
  contains it, which is then dropped, and takes no part in the pair sums: the rule of
  CEA2, see the ⚠ below. Reaction coefficients below 1e-6 are zero.
- Reaction terms: Butler and Brokaw over the pairs of the set with
  RT/(pD_ij) = 5 M_i M_j/(3 A* η_ij (M_i+M_j)), A* = 1.1, so that
  λ_re = R ΔHᵀ G⁻¹ ΔH; the reaction heat capacity is the same form without the
  RT/(pD) weights. Both linear systems are solved by `Equilibrium`'s dense solver.
- Heat capacities: the frozen and the frozen-plus-reaction heat capacity of the set per
  kilogram of the set's gas (the reference's `cp_fr` and `cp_eq` of the transport
  model when transport is on); the Prandtl numbers use them.
- Outputs: viscosity, frozen and reacting conductivity, both Prandtl numbers, both heat
  capacities, the mole fraction of the estimated species, the counts (species of the
  set, reactions, estimated species, trace eliminations), a flag for a full set, and a
  status.
- The evaluation consumes the supplied composition without independently restoring
  trace species. Retention and reporting thresholds belong to
  [Equilibrium](../Equilibrium/API.md), including its held-set policy.

⚠ 2026-11-14: this paragraph said Equilibrium zeroed every species below 1e-8;
that predates its two-stage retention and held-set rules (A28). The seeding invariant
also incorrectly claimed every zero-mole species was skipped, despite the default
component rule below. Both statements now defer to their owning policies.

⚠ 2026-09-12, a defect of the reference: cea 3.3.4 writes `continue` (a no-op in
Fortran) where CEA2 had `GOTO 260` in the elimination of a trace species, so the
reaction through the trace species is kept while its pairs are dropped, and the
reacting conductivity of a station whose component species is a trace is inflated
(170 to 440 times the frozen at the LOX/LH2 exits below 1020 K). This node keeps the
rule of CEA2; the fixtures node records the defective stations, and the test node
skips the reacting fields at committed reference-defect keys and asserts
that the defect remains visible there (the test node's API). → HISTORY.md#reference-defect-continue

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The
evaluation of one station is one public entry over internal static stage classes, all
kernel-compatible, one class per file in this directory and namespace, sharing the
existing view, scratch and figures structs. Every floating-point expression keeps its
present form and its present order of evaluation, and the stages run in the present
line order of `Evaluate` even where two of them look independent: the bit snapshot of
the tests node (the acceptance criteria below) is the proof that the decomposition
moved code and rewrote no formula.

⚠ 2026-09-15: was `TransportSolver` and the tables public, now internal, reached
through `InternalsVisibleTo` grants (see `API.md`) → HISTORY.md#visibility-internal

| Class | Responsibility | Visibility |
|---|---|---|
| `TransportSolver` | the contract: the constants, the five fit lookups (`FitOf`, `FitValue`, `PureViscosity`, `PureConductivity`, `PairViscosity`) and `Evaluate`, which builds the `StationInputs` and forwards to the composition root | internal (2026-09-15, distribution phase), contract unchanged |
| `StationEvaluation` | the composition root: the order of the stages and the status; holds no formula. Its efferent coupling is 15 since it also calls `ComponentBasis` (2026-09-28), over the root's limit of 14; the `## Shape exceptions` table below carries the measured figure | internal |
| `TransportInput` | may this station be evaluated, and how much gas it holds: the temperature, table and mole checks, the gaseous mole sum, `InvalidInput` and `NoTransportData` | internal |
| `TransportComponents` | the active element rows, each row's default species, the component of each row (with the predicates `AtomCount`, `OfCase`, `SameColumn`) | internal |
| `ComponentBasis` | the components settled before the seeding (2026-09-28): the element rows reduced over the columns of the components and the defaults, in element order, a vanished pivot reverting its row to the default species; writes `Component` and nothing the later stages read besides it | internal |
| `TransportSetSelection` | which species take part: the case's gas count, the components, the decade passes to the coverage or the cutoff, `Capped` | internal |
| `SetSpeciesProperties` | the per-species and per-pair η, λ, `Cp°/R` and `H°/RT` of the set: from the fits where there are data, from the estimates where there are none (two entries, run at the present positions 6 and 10 of the order) | internal |
| `ReactionBasis` | the stoichiometry of the set reduced so that the component columns are unit vectors (with `LocalIndex`); since 2026-09-28 it reads `Component` and no longer writes it, a zero pivot leaving its row unreduced as the reference's `tem == 0` does | internal |
| `ReactionSet` | the independent reactions among the set's species and the trace eliminations | internal |
| `MixtureRules` | the mixture viscosity and frozen conductivity, equations (5.3)–(5.7) | internal |
| `ReactionTerms` | the reaction contribution to conductivity and heat capacity (Butler and Brokaw): the enthalpy differences, the two matrices, the two dense solves, `SingularMatrix` | internal |
| `SetProperties` | the set's mass and heat capacities and the two Prandtl numbers; fills the figures | internal |

Carriers (`Carriers.cs`): `StationInputs` (the station under evaluation: its tables, its
composition, its temperature and its scratch, built once in `Evaluate` and read by
every stage, so that a stage's signature names only what is particular to it: the set
size, the reaction count, the carriers it reads and the figures it fills), `MixtureTransport` (viscosity,
frozen conductivity) and `ReactionContribution` (heat capacity, conductivity, status).
The bookkeeping the figures already carry (`SpeciesCount`, `ReactionCount`,
`EstimatedSpeciesCount`, `EstimatedMoleFraction`, `TraceEliminations`, `Capped`)
travels as `ref TransportFigures`, the stack local `Evaluate` uses today.

⚠ 2026-09-15: was `StationInputs` justified by "no stage signature exceeds four
parameters", now by what it is (the count was wrong) → HISTORY.md#stationinputs-count

Every stage's XML comment lists the scratch slots it reads and the slots it writes.
`Stx` and `Mark` are shared scratch: `Stx` is the normalised pivot row of the trace
elimination in `ReactionSet` and the per-pair difference vector in `ReactionTerms`,
valid only within each; `Mark` carries "seen by the component search" and "in the
set" (the review's F-TP-06).

Decisions taken with the review of 2026-09-14:

- **`SingularMatrix` means the frozen figures.** When a reaction system cannot be
  solved the frozen figures are written and the reacting ones equal them (`API.md`,
  Errors): `ReactionTerms` zeroes both contributions on either failure (the review's
  F-TP-01), and the tests node exercises the status through the stage. No fixture
  reaches the path. → HISTORY.md#decision-singular-matrix
- **The scratch descriptor stays.** `TransportScratch`'s constructor lists its 22
  slices; grouping them into three structs would move the contract for no run-time
  gain, and `Slice` is the only caller in the tree. The constructor is this node's
  declared exception to the parameter rule: a descriptor whose constructor enumerates
  the slices of a blittable struct. `TransportTableView`'s constructor (10 parameters)
  and `TransportTableArrays`' (9) are the same case. Every creation of the three names
  its arguments, as the root requires of a mirrored shape (added 2026-09-14: a scan of
  the construction sites found them positional).
- **`TransportLayout.DoublesPerCase` keeps its species count.** The double scratch is
  `4·M² + E·M + 8·M` with `M = MaxSpecies` and does not grow with the table; the
  parameter mirrors `ScratchLayout` so that the execution node sizes every scratch the
  same way. `API.md` says so now.
- **`TransportTable.Build`** is host code: the species runs and the pair runs become
  two private methods and `Build` the assembly.
- **Size.** No method over 60 lines and no control flow nested deeper than 3 in every
  stage; the composition root may claim the declared exception only as a plain
  sequence of stage calls under 100 lines.

As built, 2026-09-14: one file per stage class, the three carriers in `Carriers.cs`,
the two collectors of the host-side table build (`SpeciesRuns`, `PairRuns`) in
`TableRuns.cs`. `TransportComponents.OfCase` and `ReactionBasis.LocalIndex` are
internal for a stage other than their owner. The stages carry the bookkeeping into
the figures where they count it, and the composition root reads the species count back
out of them, so that no stage returns a tuple. → HISTORY.md#as-built

  ⚠ 2026-09-14: was `Run` 32 lines and `Slice` 53, now 28 and 58 (`ShapeMeasures`)
  → HISTORY.md#as-built-figures

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `TransportScratch.TransportScratch` | parameters | 22 | a descriptor whose constructor enumerates the slices of a blittable struct; grouping them into three structs would move the contract for no run-time gain (the decision "The scratch descriptor stays"); every creation names its arguments |
| `TransportTableView.TransportTableView` | parameters | 10 | the same case as `TransportScratch` above |
| `TransportTableArrays.TransportTableArrays` | parameters | 9 | the same case as `TransportScratch` above |
| `StationEvaluation` | efferent coupling | 15 | the composition root: the order of the stages and the status, now including `ComponentBasis` (2026-09-28); holds no formula |

⚠ 2026-09-28: was no `StationEvaluation` row, Ce 14 at the limit, now Ce 15 with
`ComponentBasis`, the row above → HISTORY.md#shape-row-ce15

### Audit closure (2026-11-14)

- Temperature and mole numbers must be finite as well as having their documented
  signs; the gaseous mole sum must remain finite. Invalid input leaves zero figures.
- An otherwise successful evaluation with unrepresentable numeric figures returns
  TemperatureOutOfRange with zero figures. Fit extrapolation remains the reference
  policy for representable values.
- Table upload acquires by length, publishes ownership, then copies each nonempty
  column. Allocation or copy failure rolls back every returned buffer, including
  the current column. Empty columns expose the same default empty view without a
  buffer or copy. Disposal attempts every owned buffer for recoverable failures.
  A child OutOfMemoryException is recorded and does not stop later cleanup.
  StackOverflowException and AccessViolationException propagate; actual host OOM
  is not claimed tested, and diagnostic storage under it is best effort.
- Upload rollback keeps the exact original exception object if diagnostic access or
  assignment fails with a recoverable managed exception. Read-only Data and throwing
  getters/setters may suppress secondary diagnostics, but never replace the primary.

⚠ 2026-11-14: A04's earlier primary-preservation evidence used writable diagnostic
storage only. Independent ownership review at f78f549d reproduced read-only Data
replacing the primary with NotSupportedException. Diagnostic attachment now follows
the same recoverable-exception policy as cleanup; the two CLR fatal exceptions remain
outside that guarantee. No actual memory exhaustion or driver failure is simulated.


⚠ 2026-11-14: independent ownership review of ILGPU1.5.3 found Allocate1D(array)
allocates then copies without a rollback handler. The previous outer acquisition
could not own the current column if that copy threw. Production columns now use a
private acquire/initialize sequence that registers the returned buffer before copying.
An empty ILGPU wrapper itself threw on disposal in the isolated control; default
empty views preserve the outward empty-column semantics without that wrapper.
CPU controls inject a null explicit stream only in that initializer; they inspect
actual wrapper/native disposal. This does not simulate a production null stream,
actual OOM/TDR, finalizer timing, or GPU driver recovery.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No silent estimate: every estimated species is counted and its mole fraction reported.
  A species is estimated when either its viscosity or its conductivity comes from the
  estimate (2026-09-26).

  ⚠ 2026-09-26: was only a missing viscosity counted as estimated, now either fit
  missing counts (`UF6`) → HISTORY.md#taboo-estimated-count
- No unit other than SI leaves this node; the file's units are folded into the table
  once and nowhere else.
- No re-solving of the equilibrium here: the composition is an input.
- No second dense solver: the linear systems go through `Equilibrium`'s.
- No `float`, no exceptions, no allocations: kernel code.
