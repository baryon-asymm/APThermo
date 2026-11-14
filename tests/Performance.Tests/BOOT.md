# BOOT.md — Performance.Tests

## Purpose

The definition of what "`Performance` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the invariants on a converged case: constant entropy, sonic throat, area ratio met, frozen composition, velocity from the energy equation; status on invalid exits and inputs | the invariants' tolerances | ✅ |
| L1 | rocket cases of the fixtures node (LOX/LH2 example 8, MMH/NTO example 12 equilibrium and frozen, the four reference propellants): stations, `c*`, `C_F`, `Isp`, `Ivac`, area and pressure ratios, compositions | the fixtures node's reference outputs and its tolerance table | ✅ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
| L0 | an exit station that never leaves the subsonic side is `NotConverged` and its neighbours `Ok`, driven through the `AreaRatioIteration` stage from an estimate deep on the subsonic side (2026-09-14) | the `API.md` of `Performance` | ✅ (2026-09-14) |
| L0 | `StationSolve.At` carries `NoInlining`, so ILGPU compiles one copy of the equilibrium solve for the rocket program's seven call sites (2026-09-30, `CompileSizeTests.TheStationSolveIsNotInlined`; the compile's allocation bound is the execution tests node's) | the compiled method's implementation flags | ✅ (2026-09-30) |
| Bits | the host solve of every rocket fixture gives the recorded bits: one line per fixture in `Bits.approved.txt`, the fixture's path and the SHA-256 of the raw bits of the stations' states, moles, multipliers, figures, station statuses, iteration counts and the case status, in that order | the approved snapshot, recorded at `8e36a27` before the decomposition of 2026-09-14 | ✅ (2026-09-14) |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- **Reference is files**; **tolerances come from the fixtures node**; **every fixture
  case is enumerated**, as in the equilibrium tests node.
- The fixture's numeric fields map by reflection to MixtureState or
  PerformanceFigures; an unmapped fixture field fails loudly. The finite-figure
  invariant separately enumerates every double property of PerformanceFigures.

  ⚠ 2026-11-14: the old wording promised reverse coverage of newly added result
  fields. StationComparison is fixture-driven, so it never established that claim
  (A28); its actual scope and the independent finite-field inventory are stated here.
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  numerics against unnoticed change the way the surface snapshot guards the contract
  (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is legitimate only with the
  numerical change that moved it named in the same commit; a decomposition, a
  renaming or a reordering of code moves no line. The snapshot is of the CPU
  accelerator on the reference machine's runtime; a runtime update that moves lines
  is re-approved with that reason recorded here. A fixture absent from the snapshot
  fails the test with instructions, as the surface snapshot does; a line of the
  snapshot that names no current fixture fails a test of its own instead of staying
  silent (`EveryApprovedLineNamesARocketFixture`, 2026-09-15).

  ⚠ 2026-10-05: was a Windows and a Linux record, now one `Bits.approved.txt` picked by
  `Harness.ApprovedSnapshot.RecordPathFor`, the tree's own `Exp`, `Log` and `Pow` making the
  platforms equal → tests/Harness/HISTORY.md#one-record-2026-10-05

  ⚠ 2026-09-17: was one snapshot file, now a Windows and a Linux record, picked by
  `Harness.ApprovedSnapshot.ApprovedPathFor` → HISTORY.md#bits-per-platform

  ⚠ 2026-09-19: was bits as a record of the platform, now of the reference machine:
  `EveryRocketFixtureGivesTheRecordedBits` and `EveryApprovedLineNamesARocketFixture`
  carry `[Trait("Category", "BitSnapshot")]`, filtered out of the hosted fast suite
  → HISTORY.md#bits-reference-machine

  ⚠ 2026-10-05: was "filtered out of the hosted fast suite" (`Category!=LongRunning&Category!=BitSnapshot`),
  now the hosted runs filter `Category!=LongRunning` only and the two bit facts run on every
  runner, the hosted ones included, against the one `Bits.approved.txt` (root BOOT.md, Platform)
- The node owns the tolerances of comparisons that are not with the reference: the
  invariants' tolerances and the self-consistency and identity tolerances are named
  constants of the node with their origin in a comment, never literals in an
  assertion (2026-09-14).
- **This node keeps its own reader of a fixture's outputs** (2026-09-14, the
  architecture review's F-AR-03): the field-name mapping (`StationComparison.Fields`)
  and the set of fields that belong to another node (`TransportFields`) stay here, not
  in the harness, which holds no formula and no tolerance. This node reads a station
  with performance figures on top of the state `Equilibrium.Tests` reads alone, and
  `Problems.Tests` reads a station with transport figures on top of that; a shared
  reader would have to know all three shapes, which would put it above the nodes its
  readers' own consumers test. Only the trace-threshold selection line moved out, to
  the fixtures node's `ToleranceTable.MoleFractionField` (the same F-AR-03 finding: it
  stood typed, with its selection line, in this node and in `Equilibrium.Tests` and
  `Problems.Tests` alike).

## Dependencies

- [Performance](../../src/Performance/API.md) — what is being checked.
- [Equilibrium](../../src/Equilibrium/API.md) — scratch layout for the solves.
- [Thermo](../../src/Thermo/API.md) — tables.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.
- [Harness](../Harness/API.md) — the CPU host, bit comparison and fixture families.

Outside the tree: xunit; ILGPU 1.5.3 (CPU accelerator only).

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; no writes into the working directory.
- The exits of a fixture are its pressure ratios, then its supersonic area ratios, in
  the reference's station order; subsonic area ratios (one station of example 8) are
  outside version 1 and are left out of the solver's stations and of the comparison.
- Left out of the comparison by the fixtures node's caveats: `cvFrozen` and
  `cvEquilibrium` at frozen stations, `cpFrozen` and `cvFrozen` at a station with
  condensed species when transport is on (gas-phase values in the reference). A
  species below the reference's print threshold of 5e-6 is compared with the
  `moleFractionTrace` entry, the others with `moleFraction`.
- The batch struct of the kernel test is public; a batch is a family of fixtures
  sharing a table and an exit layout.

  ⚠ 2026-09-15 (distribution phase): was "ILGPU compiles kernels only over public
  parameter types", now `InternalsVisibleTo("ILGPURuntime")` suffices; the struct stays
  public → HISTORY.md#kernel-struct-public

- **No NaN-blind predicate** (2026-09-26, the guards audit of 2026-09-26 (`Audit 5`, the hidden-defect audit's fifth part), F5 and F6):
  - Every tolerance predicate of this node is written so that NaN fails it
    (`!(|a − b| <= tol)`), or asserts finiteness first. This covers
    `RocketInvariants` and `StationComparison`, and every other predicate the coder
    finds by searching for `> ` against a tolerance.
  - A fact asserts `Velocity == SpecificImpulse` and both finite at every `Ok` station
    of every fixture, since the fixtures carry no `velocity` to compare with.
  - The frozen-station cv skip applies only where the reference shows the defect's
    signature: `cvFrozen` and `cvEquilibrium` 0, or the freezing station's values.
    Anywhere else the fields are compared.
  - ⚠ `state.Velocity = NaN` at every station left this node 601/601 green, and a
    fixture's frozen exit cv edited from 0 to 2500 and 9999 left it green too.

## Shape exceptions

Added 2026-09-14 by the design session, after the protocol tests node's measurements found
this constructor over the root's six parameters. This node's own `RocketBatchViews` is the
parameter struct of one rocket kernel launch, one argument per view of the batch, as ILGPU
takes a kernel's arguments and as the execution node's struct of the same name is declared;
grouping the views would re-shape the launch the kernel equality tests mirror. On the
root's condition for such a type its creation names its arguments; it passes them by
position today (the criterion below).

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `RocketBatchViews.RocketBatchViews` | parameters | 16 | the parameter struct of one rocket kernel launch, one argument per view; its creation names its arguments |

### Snapshot diagnostics correction (2026-11-14)

⚠ 2026-11-14: a snapshot mismatch formerly exposed only a digest. The caller must
pass actual named values through ApprovedSnapshot.Problem's existing fields argument:
station index, reflected property name, species/element identity for moles/multipliers,
station status, iteration count and case status. Keep the hash's exact traversal and
approved bytes; labels are diagnostic text only, never extra hash input.

### Audit closure (2026-11-14)

- Only the known BeO plateau-edge mismatch exempts its seven derivative, sound and
  Mach fields, guarded by that fixture and its pinned-pair signature. Ordinary
  sonic throats and the other fields remain compared.
- Finite-figure checks enumerate double properties of PerformanceFigures; a
  nonfinite value in every property is rejected by a negative control.
- The first-maximum oracle rejects a candidate downstream of any distinct earlier
  local maximum, even when that candidate is higher. A synthetic two-peak profile
  proves the ordering predicate.
- A policy-constant test states only its policy scope; execution of tail steps
  is not inferred from reading the constants.
- Test review T13 also requires a solver-level boundary case whose physical throat
  state agrees with an independent sonic reference and distinguishes the configured
  two-step tail from a mutation that skips its execution while leaving constants
  intact. Prefer a single ideal-gas case with an analytic sonic relation; preserve
  existing numerical tolerances and snapshots. A private-method reflection call or
  source-text count is not a substitute for the solved-state assertion.


### T13 sonic reference (2026-11-14)

The new solver-level guard uses a single gaseous product without reactions or phase
changes. Argon at 600 K stays within the 200-1000 K constant-Cp fit, checked
from its NASA coefficients against the analytic perfect-gas sonic ratios. N2 at
3000 K supplies the variable-Cp boundary
case: independently integrate the raw NASA Cp polynomial with composite Simpson
quadrature (256 and 512 panels), bisect the energy/sound-speed balance in
temperature, then integrate Cp/T for the isentropic pressure ratio. With c=Cp/R
and integrals over [T*, Tc], the reference solves
2 integral(c dT) = c(T*)/(c(T*)-1) T*, and ln(p*/pc)=-integral(c/T dT).
The reference uses no production H/S,
station solve, throat update or actual reported state as an expectation. Mesh
refinement and the analytic control must meet the existing 1e-12 pressure-ratio
identity budget before comparison; retain the existing polish tolerance for the
solved chamber and throat. Frozen flow with one candidate forbids dissociation and
composition changes; the sonic interval stays inside one NASA fit. Production H
sets the chamber input only, which must independently recover its prescribed T.
A scratch compilation overlay skips only the tail-loop execution, preserving both
policy constants and checked-in source. Its red physical-state assertion and the
normal positive solve are required evidence; the constant fact alone is insufficient.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing case.
- Do not compare a subset of fields chosen by hand.
