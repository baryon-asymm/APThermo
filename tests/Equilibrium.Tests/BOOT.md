# BOOT.md — Equilibrium.Tests

## Purpose

The definition of what "`Equilibrium` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the internal dense solver on small systems; element conservation of a converged result; status codes on invalid input; the absent-element mask | analytic solutions; the invariant's tolerance; a table without the element | ✅ |
| L1 | tp, hp and sp solves for the fixture mixtures: composition, temperature, `M`, `MW`, `Cp_eq`, `γ_s`, sound speed; condensed species inclusion (AP/binder/aluminium, RP-1311 example 14); frozen mode | the fixtures node's reference outputs and its tolerance table; the frozen stations of the reference rocket cases | ✅ |
| L1 and L2 | the pinned set: hp and sp states inside the Al(OH)3/Al2O3/H2O(L) band end `Ok` at the plateau temperature with the reference's condensed set, the plateau convention holds, `γ_s` and `(∂ln V/∂ln p)_T` match finite differences, a `DerivativeSystem` on three dependent species is `Solved` and pinned | the `seeded` fixtures, solved as the reference was (tp at 430 K, then the case warm-started); the tree's own finite differences; the dependence of the element vectors | ✅ 2026-10-03 |
| L1 and L2 | the gas-participating plateau and the near-univariant sliver: seeded hp and sp states of CaCO3/CaO under CO2 end `Ok` at the plateau temperature with the pinned convention, `γ_s` and `a` against the isentropic finite difference and, at 1e4 and 1e5 Pa, the closed form of a pure gas; an sp march into the two-phase region of water; one state each of NH4Cl, Ca(OH)2 and MgCO3; the sliver's `γ_s` at `Cp/Cv` above 1e10; continuity of `γ_s` to the exact plateau | the tree's own finite differences of its sp results; the closed form `1/(1 − 2/L + c/L²)` with `L` from the species functions; the plateau temperature bisected on the tp states' condensed set (`UnivariantRig`) | ✅ CPU 2026-10-03; CUDA/Linux evidence is owned by StateRecord/BOOT.md |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
| L2 | states the reference cannot reach: the pinned pair at a cut, the refusal where no admissible set exists, no condensed candidate with positive gain left out of an `Ok` status | the node's own condensed-species rule (`BOOT.md`), not the reference | ✅ |
| Bits | the host solve of every tp, hp and sp fixture case gives the recorded bits: one line per case in `Bits.approved.txt`, the case file and the SHA-256 of the raw bits of the moles, the multipliers, every field of the state, the status and the iteration count, in that order | the approved snapshot, recorded at `8e36a27` before the decomposition of 2026-09-14 | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

⚠ 2026-11-14: the table said the StateRecord CUDA/Linux criterion was still open.
Its owning [acceptance criteria](../../src/Equilibrium/StateRecord/BOOT.md#acceptance-criteria)
record the completed proofs; this table now links readiness to that owner rather
than keeping a stale copy. This correction records no new hardware measurement.
## Invariants

- **Reference is files**: fixture cases carry element moles, pressure, target, the
  reference composition and properties; nothing is typed into tests.
- **Tolerances come from the fixtures node** (one table for all comparisons with the
  reference) and are not overridden locally.
- **Every fixture case is compared**: the test enumerates the fixture directory; a new
  fixture file is a new test case without code changes.
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  numerics against unnoticed change the way the surface snapshot guards the contract
  (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is legitimate only with the
  numerical change that moved it named in the same commit; a decomposition, a
  renaming or a reordering of code moves no line. The snapshot is of the CPU
  accelerator on the reference machine's runtime; a runtime update that moves lines
  is re-approved with that reason recorded here. A fixture case absent from the
  snapshot fails the test with instructions, as the surface snapshot does.
  Each actual mismatch passes named mole/species, multiplier/element, reflected state
  property, status and iteration values to the Harness per-case field dump. Hash
  order and approved bytes remain unchanged.

  ⚠ 2026-11-14: T16 found this node supplied only a digest despite Harness supporting
  field dumps. Numeric failures now also emit the named actual values beside the
  actual snapshot; the approval and hashing contracts are unchanged.

  ⚠ 2026-10-05: the Windows and the Linux record named below are one record now (owner
  decision O5, item 13 of release 0.2.2). The tree's own correctly rounded `Exp`, `Log`
  and `Pow` replaced the platform's C runtime in every kernel, so the Windows and the
  Linux (WSL2) bits of this node are equal and `ApprovedPath` resolves through
  `Harness.ApprovedSnapshot.RecordPathFor` to `Bits.approved.txt` on every platform;
  `Bits.linux.approved.txt` is deleted.

  ⚠ 2026-09-17: this bullet assumed one snapshot file. The root's platform constraint
  now keeps a Windows and a Linux record, since the CPU accelerator's `System.Math`
  calls the platform's C runtime and the two do not round the last bit alike;
  `ApprovedPath` resolves through `Harness.ApprovedSnapshot.ApprovedPathFor`
  (`tests/Harness/API.md`), which picks `Bits.approved.txt` or `Bits.linux.approved.txt`
  for the running platform, so this node's own code names no platform. The first Linux
  run (2026-09-17, WSL2 Ubuntu 24.04, `f67b1a9` plus this task's harness change) did not
  reproduce the Windows bits, within the tolerance the root BOOT.md records for the
  difference; `Bits.linux.approved.txt` was approved from that run.

  ⚠ 2026-09-19: the root BOOT.md's platform constraint (⚠ 2026-09-18, the declared
  deviation from "every test runs on both platforms") holds that the bits are a
  record of the reference machine, not of the platform alone: hosted CI runners land
  on CPUs whose C runtime rounds the last bit differently from the reference machine.
  `EveryFixtureCaseGivesTheRecordedBits` now carries
  `[Trait("Category", "BitSnapshot")]`, so it runs in every local run (`CLAUDE.md`'s
  fast set) and in the release's self-hosted jobs
  (`.github/workflows/release.yml`'s `cuda-windows` and `cuda-linux`, filter
  `Category=Cuda|Category=BitSnapshot`), and is filtered out of the hosted fast suite
  (`ci.yml`; `release.yml`'s `matrix` job; filter
  `Category!=LongRunning&Category!=BitSnapshot`), where the CEA tolerance tests of
  the L1 row above hold correctness instead.

  ⚠ 2026-10-05: was "filtered out of the hosted fast suite" (filter
  `Category!=LongRunning&Category!=BitSnapshot`), now the hosted runs filter
  `Category!=LongRunning` only and the bit facts run on every runner, the hosted ones
  included, against the one `Bits.approved.txt` (root BOOT.md, Platform).
- **This node owns the tolerance of a comparison that is not with the reference**
  (2026-09-14): two paths of this tree reaching the same state (an equilibrium solve
  and a frozen one at its composition; a plateau state reached twice) or an algebraic
  identity of one state have no entry in the fixtures node's table to ask, so their
  tolerance is a named constant of this node, `Tolerances.cs`, with its origin in a
  comment, rather than a literal at the assertion (F-TK-10).

  ⚠ 2026-09-15: read unconditionally, while `DenseSolverTests` compares the internal
  dense solver against hand-solved analytic systems with a literal bound at each
  assertion (`1e-14`, `1e-15`, `1e-9`), not against another path of this tree. Found by
  the repair review (R-Equilibrium.Tests-3): those are unit facts of a small algebraic
  routine, one system apiece, outside the two-paths and identity cases this invariant
  names — naming each its own constant in `Tolerances.cs` would turn a policy of the
  node into a list of one-off numbers with nothing to compare against. The invariant now
  excludes `DenseSolverTests`; its bounds stay at their assertions, each with its origin
  in a comment beside it.
- **This node keeps its own reader of a fixture's outputs** (2026-09-14, the
  architecture review's F-AR-03): the field-name mapping (`StateComparison.StateFields`)
  and the set of fields that belong to another node (`TransportFields`) stay here, not
  in the harness, which holds no formula and no tolerance. `Performance.Tests` reads a
  station with performance figures on top of the state this node reads alone, and
  `Problems.Tests` a station with transport figures on top of that; a shared reader
  would have to know all three shapes, which would put it above the nodes its readers'
  own consumers test. Only the trace-threshold selection line moved out, to the
  fixtures node's `ToleranceTable.MoleFractionField` (the same F-AR-03 finding: it stood
  typed, with its selection line, in this node and in `Performance.Tests` and
  `Problems.Tests` alike).

## Dependencies

- [Equilibrium](../../src/Equilibrium/API.md) — what is being checked.
- [Newton](../../src/Equilibrium/Newton/API.md), [Condensed](../../src/Equilibrium/Condensed/API.md) and
  [StateRecord](../../src/Equilibrium/StateRecord/API.md) — the child nodes of `Equilibrium`, whose stage
  types its unit facts drive (`NewtonLoopState`, `SingularRemedies`, `PhaseGeometry`, `MixtureProperties`).
- [GasPhase](../../src/Equilibrium/GasPhase/API.md) — the gasless verdict, whose `Decide`, simplex and tangent-plane
  search its unit facts drive (`GasPhaseVerdict`, `CondensedSimplex`, `TangentPlane`).
- [Recovery](../../src/Equilibrium/Recovery/API.md) — the temperature bracket and the attempt plan, whose pure
  transitions its unit facts drive (`TemperatureBracket`, `BracketMove`, `EndKind`, `AttemptPlan`).
- [TraceGas](../../src/Equilibrium/TraceGas/API.md) — the trace-gas Newton pass, whose stage types its unit and
  family facts drive (`TraceGasPass`, `TraceGasSystem`, `TraceGasStep`, `TraceGasStart`, `PhaseOneSeed`).
- [Thermo](../../src/Thermo/API.md) — building the tables of the fixture species lists.
- [Data](../../src/Data/API.md) — loading the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.
- [Harness](../Harness/API.md) — the CPU host and the bit snapshot mechanics.

Outside the tree: xunit; ILGPU 1.5.3 (CPU accelerator only); Microsoft.CodeAnalysis.CSharp 5.0.0.

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; no writes into the working directory.
- The species list of a fixture case is the reference's product list, so that the
  comparison is of solvers, not of selection rules (selection is `Problems`' concern).
- The fields compared are read from the fixture: every numeric output with a
  `MixtureState` field of the same name (the transport fields belong to `Transport`);
  a species below the reference's print threshold of 5e-6 is compared with the
  `moleFractionTrace` entry, the others with `moleFraction`. At a frozen rocket
  station `cvFrozen`, `cvEquilibrium` and `mach` are not compared (the reference computes
  no Cv there; see the Fixtures `BOOT.md`), and `cpEquilibrium` is compared with the
  node's frozen Cp.
- Frozen mode is checked on the frozen stations of the reference rocket cases: the
  composition of the last equilibrium station (moles from its mole fractions and `M`)
  is expanded at constant entropy to each frozen station's pressure.
- The dense solver is internal to `Equilibrium` and reached through
  `InternalsVisibleTo`; the batch struct of the kernel test is public.

  ⚠ 2026-09-15 (distribution phase): this bullet gave the reason "because ILGPU
  compiles kernels only over public parameter types". Wrong: ILGPU 1.5.3 needs only
  `[assembly: InternalsVisibleTo("ILGPURuntime")]` on the declaring assembly to load an
  internal kernel parameter type, method or view element; public is only one way to
  satisfy it (the API review of 2026-09-15, section 3, fixed here in `c11e02b`,
  proved it on the CPU accelerator and on CUDA — Execution's own four views structs
  are internal now, with that grant). The claim entered with `f2e5de7`/`53ec9fb` on
  2026-09-12. The struct stays public here; nothing in this node's own scope required
  the change.

- **No NaN-blind predicate, and the frozen cv skip keyed on the reference**
  (2026-09-27, the guards audit of 2026-09-26 (`Audit 5`, the hidden-defect audit's fifth part), F5 and F6). The rules are those of the performance tests node:
  - Every predicate of this node that compares a value with a bound is written so that
    NaN fails it (`!(x <= bound)`), or asserts finiteness first. This covers
    `ElementConservationTests`, and every other predicate the coder finds by searching
    for `> ` against a bound or a tolerance.
  - `FrozenModeTests` skips `cvFrozen` and `cvEquilibrium` at a frozen station only
    where the reference shows the defect's signature: both 0, or both equal to the
    freezing station's values. Anywhere else the two fields are compared with the
    table like any other.
  - ⚠ A NaN residual passed `ElementConservationTests`, since `NaN > bound` is false.
    A frozen exit's cv edited in a fixture copy, from 0 to 2500 and 9999, left this node
    463/463 green: the fields were skipped for every frozen station. Today all 124
    frozen stations of the fixtures carry the signature (109 exits at 0/0, 15 throats
    with the chamber's values, counted by the audit), so no comparison changes.

## Shape exceptions

Added 2026-09-14 by the design session, after the protocol tests node's measurements found
this constructor over the root's six parameters. `BatchViews` is the parameter struct of one
kernel launch, one argument per view of the batch, as ILGPU takes a kernel's arguments and
as the execution node's own views structs are declared; grouping the views would re-shape
the launch the kernel equality tests mirror. On the root's condition for such a type its
creation names its arguments; it passes them by position today (the criterion below).

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `BatchViews.BatchViews` | parameters | 13 | ⚠ 2026-11-14 R10: was 12, now 13 after appending the isolated typed recovery-state view; the descriptor only projects fields and its creation names its arguments |

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing fixture case.
- Do not compute expectations with the code under test.
- Do not skip when the fixtures are missing.
