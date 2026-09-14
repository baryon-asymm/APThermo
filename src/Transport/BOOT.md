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

  ⚠ 2026-09-13: the set's rule (Constraints) counted "the gaseous species in the
  table", as the reference counts the gaseous products of its problem, and the two
  agree only in a table built for one case. In the front door's batch over a union
  of elements the larger count lowered every threshold: the LOX/RP-1 throat set took
  a fourteenth species in the table shared with AP/HTPB/Al, and its figures moved by
  up to 4e-7 relative against the single solve. Found by the front door's batch test;
  the count is now the case's, and the test node proves the invariant bit for bit.
- **Stateless, deterministic, no allocation**, as every numerical node.

⚠ 2026-09-12: the second invariant stood "Only species with data take part. A species
without a transport entry is excluded from the mixture rules and the mole fractions of
the remaining species are renormalized, as CEA does; … a station where [the excluded
fraction] exceeds the threshold below is `NoTransportData`", with a taboo "No default
transport properties for species without data: absence is reported, never guessed".
Wrong: the reference (cea 3.3.4, `compute_transport_properties` in
`source/equilibrium.f90`, as CEA2's `TRANIN` before it) keeps such species and
estimates their viscosity by hard spheres and their conductivity by the modified Eucken
relation. Found when the AP/HTPB/Al chamber, where 2.8 % of the gaseous moles belong to
species without data (`ALCL`, `CL`, `ALOH`, `ALOHCL2`, …), could not be reproduced by
exclusion (−0.9 % viscosity, +3.3 % frozen conductivity, −5.6 % reacting Prandtl in the
Python prototype of this node) and was reproduced to 3e-8 with the estimate. The
threshold and the exclusion are dropped; the number of estimated species and their mole
fraction are reported instead.

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

⚠ 2026-09-12: the sketch took "the mole numbers and Lagrange multipliers of the
station, and the equilibrium derivatives" from `Equilibrium` for the reaction term.
The reaction contribution needs neither the multipliers nor the derivatives: it is
built from the stoichiometry of the set (independent reactions among its species) and
the species enthalpies, as the report does; the multipliers left the signature.

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
  independent of the rows' default species, the monatomic gases); then every gaseous
  species with moles not below n/(ng·10^k), k = 1, 2, …, until the set carries
  (1 − 1e-9)(1 − 1e-6) of the gaseous moles n, the set is full, or the threshold falls
  below 1e-11 n (ng is the number of gaseous species of the case: those of the table
  whose every element the case holds, which in a table built for the case alone is
  the table's gas count, the reference's product list; see the Invariants for the
  correction of 2026-09-13). Within a pass the
  species are taken in table order, which matters only when the set fills: the
  AP/HTPB/Al chamber needs 56 species for the coverage and takes the first 40. Mole
  fractions x_s are relative to the set.
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
- Reactions: the components are those of the seeding; the stoichiometry of the set is
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
- Not done: the reference restores gaseous species down to x ≈ e^−25.33 ≈ 1e-11 of
  the gas before the selection; this node takes the moles it is given, and
  `Equilibrium` reports species below 1e-8 of the gas as zero. The difference is
  confined to species below 1e-8 and to the coverage test and lies below every
  tolerance of the fixtures.

⚠ 2026-09-12, a defect of the reference: cea 3.3.4 writes `continue` (a no-op in
Fortran) where CEA2 had `GOTO 260` in the elimination of a trace species, so the
reaction through the trace species is kept while its pairs are dropped, and the
reacting conductivity of a station whose component species is a trace is inflated:
170 to 440 times the frozen conductivity at the LOX/LH2 exits below 1020 K, where `OH`
seeds the oxygen row at x_s < 1e-10. Found by the Python mirror of the routine
(scratch work of the session), which reproduces those values to 1e-16 with the no-op
and gives λ_eq = λ_fr with the rule of CEA2. This node keeps the rule of CEA2; the
fixtures node records the defective stations, and the test node skips the reacting
fields where the solver reports a trace elimination and asserts that the defect is
still visible there.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The
evaluation of one station is one public entry over internal static stage classes, all
kernel-compatible, one class per file in this directory and namespace, sharing the
existing view, scratch and figures structs. Every floating-point expression keeps its
present form and its present order of evaluation, and the stages run in the present
line order of `Evaluate` even where two of them look independent: the bit snapshot of
the tests node (the acceptance criteria below) is the proof that the decomposition
moved code and rewrote no formula.

| Class | Responsibility | Visibility |
|---|---|---|
| `TransportSolver` | the contract: the constants, the five fit lookups (`FitOf`, `FitValue`, `PureViscosity`, `PureConductivity`, `PairViscosity`) and `Evaluate`, which builds the `StationInputs` and forwards to the composition root | public, contract unchanged |
| `StationEvaluation` | the composition root: the order of the stages and the status; holds no formula. Named here as the composition root the root's Ce rule allows above 10 (about 14 after the split) | internal |
| `TransportInput` | may this station be evaluated, and how much gas it holds: the temperature, table and mole checks, the gaseous mole sum, `InvalidInput` and `NoTransportData` | internal |
| `TransportComponents` | the active element rows, each row's default species, the component of each row (with the predicates `AtomCount`, `OfCase`, `SameColumn`) | internal |
| `TransportSetSelection` | which species take part: the case's gas count, the components, the decade passes to the coverage or the cutoff, `Capped` | internal |
| `SetSpeciesProperties` | the per-species and per-pair η, λ, `Cp°/R` and `H°/RT` of the set: from the fits where there are data, from the estimates where there are none (two entries, run at the present positions 6 and 10 of the order) | internal |
| `ReactionBasis` | the stoichiometry of the set reduced so that the component columns are unit vectors (with `LocalIndex`) | internal |
| `ReactionSet` | the independent reactions among the set's species and the trace eliminations | internal |
| `MixtureRules` | the mixture viscosity and frozen conductivity, equations (5.3)–(5.7) | internal |
| `ReactionTerms` | the reaction contribution to conductivity and heat capacity (Butler and Brokaw): the enthalpy differences, the two matrices, the two dense solves, `SingularMatrix` | internal |
| `SetProperties` | the set's mass and heat capacities and the two Prandtl numbers; fills the figures | internal |

Carriers (`Carriers.cs`): `StationInputs` (the species view, the transport view, the
scratch, the moles and the temperature, built once in `Evaluate`; every stage takes
it, so no stage signature exceeds four parameters), `MixtureTransport` (viscosity,
frozen conductivity) and `ReactionContribution` (heat capacity, conductivity, status).
The bookkeeping the figures already carry (`SpeciesCount`, `ReactionCount`,
`EstimatedSpeciesCount`, `EstimatedMoleFraction`, `TraceEliminations`, `Capped`)
travels as `ref TransportFigures`, the stack local `Evaluate` uses today.

Every stage's XML comment lists the scratch slots it reads and the slots it writes.
`Stx` and `Mark` are shared scratch: `Stx` is the normalised pivot row of the trace
elimination in `ReactionSet` and the per-pair difference vector in `ReactionTerms`,
valid only within each; `Mark` carries "seen by the component search" and "in the
set" (the review's F-TP-06).

Decisions taken with the review of 2026-09-14:

- **`SingularMatrix` means the frozen figures.** The contract (`API.md`, Errors) says
  that when a reaction system cannot be solved the frozen figures are written and the
  reacting ones equal them. The code obeyed it for the conductivity and not for the
  heat capacity when only the second solve failed (the review's F-TP-01):
  `ReactionTerms` zeroes both contributions on either failure, and the tests node
  exercises the status through the stage. No fixture reaches the path, so the bit
  snapshot does not move.
- **The scratch descriptor stays.** `TransportScratch`'s constructor lists its 22
  slices; grouping them into three structs would move the contract for no run-time
  gain, and `Slice` is the only caller in the tree. The constructor is this node's
  declared exception to the parameter rule: a descriptor whose constructor enumerates
  the slices of a blittable struct. `TransportTableView`'s constructor (10 parameters)
  and `TransportTableArrays`' (9) are the same case.
- **`TransportLayout.DoublesPerCase` keeps its species count.** The double scratch is
  `4·M² + E·M + 8·M` with `M = MaxSpecies` and does not grow with the table; the
  parameter mirrors `ScratchLayout` so that the execution node sizes every scratch the
  same way. `API.md` says so now.
- **`TransportTable.Build`** is host code: the species runs and the pair runs become
  two private methods and `Build` the assembly.
- **Size.** No method over 60 lines and no control flow nested deeper than 3 in every
  stage; the composition root may claim the declared exception only as a plain
  sequence of stage calls under 100 lines.

As built, 2026-09-14. One file per stage class, named after it; the three carriers
share `Carriers.cs`, as decided above, and the two collectors of the host-side table
build (`SpeciesRuns`, `PairRuns`, which keep `AppendSpeciesRuns` and `AppendPairRuns`
within the parameter rule) share `TableRuns.cs` beside the other descriptors
(`Descriptors.cs`). Two predicates are called from a stage other than the one that
owns them, and are internal for it: `TransportComponents.OfCase` from
`TransportSetSelection` (the case's gas count asks the same question as the default
species of a row) and `ReactionBasis.LocalIndex` from `ReactionSet`. The stages carry
the bookkeeping into the figures where they count it — `TransportSetSelection` the
species count and `Capped`, `SetSpeciesProperties.Fits` the estimated species and
their fraction, `ReactionSet` the reaction count and the trace eliminations — and the
composition root reads the species count back out of them, so that no stage returns a
tuple. `StationEvaluation.Run` is 32 lines and holds no formula; the largest type of
the node is now `TransportComponents` at 244 lines and the largest method
`TransportScratch.Slice` at 53, against 794 and 640 before.

## Acceptance criteria

- [x] 2026-09-14 — Every rocket fixture run with transport (enumerated by the tests
      node) reproduces viscosity, frozen and reacting conductivity, both Prandtl
      numbers and the reference's `cpFrozen` on the reference composition within the
      tolerance table; the reacting fields are skipped at the nine defective stations,
      where the test asserts the defect is still visible (`Transport.Tests`,
      `StationTests.Station_figures_match_the_reference`,
      `StationTests.The_reference_cpFrozen_is_the_transport_set_heat_capacity`,
      `StationTests.Reacting_conductivity_is_never_below_the_frozen_one`,
      `StationTests.The_trace_component_stations_carry_the_documented_reference_defect`;
      green on the decomposed code at `5cb2664`). Re-dated from 2026-09-12, when the
      evidence was one test, `StationTests.Stations_match_the_reference`, over 39
      files of 4 to 11 stations with the worst deviation 3.4e-8 relative: the tests
      node split that test (its F-TK-05) and left the typed counts out (F-TK-03), and
      this node's code was decomposed on this date, which a tick of an earlier date
      cannot prove (AGENTS.md §6).
- [x] 2026-09-12 — The 27 fit fixtures (pure species and pairs) reproduce the
      independent Python evaluation within `transportFit`, the fit intervals of the
      table equal the file's, the fit rule agrees with the fixture on the shared bounds
      (`FitTests.Fit_values_match_the_independent_evaluation`,
      `FitTests.Fit_intervals_are_those_of_the_file`).
- [x] 2026-09-12 — The estimate for species without data is exercised by the
      AP/HTPB/Al stations (26 of the 40 species of the chamber set) and the reported
      count and mole fraction are positive there; the station comparison above proves
      the estimate right to 3e-8
      (`StationTests.Species_without_data_are_estimated_on_the_aluminized_propellant`,
      `FitTests.Species_without_an_entry_have_no_fits_and_are_listed`). The
      criterion stood "the exclusion policy is exercised … and the reported excluded
      fraction equals the independently computed one"; rewritten with the ⚠ above.
- [x] 2026-09-12 — Runs unchanged inside an ILGPU kernel on the CPU accelerator with
      the same bits as the host call (`KernelEqualityTests`, five batches).
- [x] 2026-09-13 — Every station of a case evaluated in a table that also holds the
      species of elements the case lacks gives the same bits as in the case's own
      table (`AbsentElementTests.A_table_with_the_species_of_absent_elements_gives_the_same_bits`:
      LOX/RP-1 in the table with AP/HTPB/Al, LOX/LH2 with N2O4/UDMH, N2O4/UDMH with
      AP/HTPB/Al, every station with transport, every field of the figures); seen red
      with the table's gas count in the thresholds on two of the three pairs (the
      LOX/RP-1 throat set of 14 species against 13, the N2O4/UDMH sets of 21 against
      26, every figure moved).
- [x] 2026-09-14 — The decomposition of `## Structure`: every type of the node within
      the root's code-shape constraint — measured over the node after the last step,
      the largest type `TransportComponents` 244 lines and the largest method
      `TransportScratch.Slice` 53 (the scratch descriptor, the declared exception to
      the parameter rule), no control flow nested deeper than 3, no method with more
      than six parameters — the public surface unchanged
      (`Protocol.Tests.SurfaceTests` green against a `PublicSurface.approved.txt` that
      did not move a line, every new type internal), and every station's figures bit
      for bit those of `8e36a27` on the CPU accelerator: the tests node's
      `Bits.approved.txt`, recorded before the first line of code moved, unchanged
      through all eight steps, with `KernelEqualityTests`, `AbsentElementTests` and
      every criterion above green after each of them, and the whole fast suite green
      at the end (2144 tests). The protocol tests node's `ShapeTests`, which the
      criterion named, does not exist yet; it re-measures this over the tree when that
      node has it.
- [ ] The execution tests node's CUDA sweep green once after the decomposition (the
      long-running `CudaTests`, which this node's session does not run: the criterion
      above was split on 2026-09-14 so that what is proven and what is still owed are
      not one tick).
- [x] 2026-09-14 — `SingularMatrix` writes the frozen figures and reacting figures
      equal to them, as `API.md` promises:
      `Transport.Tests.StatusTests.A_reaction_system_that_cannot_be_solved_keeps_the_frozen_figures`
      drives `ReactionTerms` (through `InternalsVisibleTo`) over a set of three species
      and two reactions whose second system is singular while the first is not — the
      third species weighs nothing, so RT/(pD) vanishes for both pairs that hold it and
      the one pair left gives the two reactions the same difference vector — and
      asserts the three equalities and the status. Seen red against the code of
      `8e36a27` (moved here unchanged before the fix): the status and the conductivity
      were right and the equilibrium heat capacity was 10441.86 against the frozen
      5001.70. `The_same_set_is_solved_when_every_pair_carries_a_diffusion_weight`
      keeps the first test from passing because both systems fail.

## Taboos

- No silent estimate: every estimated species is counted and its mole fraction reported.
- No unit other than SI leaves this node; the file's units are folded into the table
  once and nowhere else.
- No re-solving of the equilibrium here: the composition is an input.
- No second dense solver: the linear systems go through `Equilibrium`'s.
- No `float`, no exceptions, no allocations: kernel code.
