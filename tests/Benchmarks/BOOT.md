# BOOT.md — Benchmarks

## Purpose

Measures how fast the library computes, so that a change of the code, the clean-code
pass of 2026-09-14/15 first, can be compared against the code before it. The
bit-for-bit guards prove that the numbers did not change; nothing proves the speed.
Extracting kernel stages can change inlining on the CPU accelerator and in the
NVVM-generated CUDA code. This node is a BenchmarkDotNet console project, run by
hand, never by `dotnet test`. It records figures and asserts none.

## Invariants

- **Figures are recorded, never asserted.** No test of the tree compares a timing with
  a bound: a wall-clock assertion reddens on a busy machine for no defect (AGENTS.md
  §13, a perpetually red check). A regression is found by reading the comparison, and
  it goes to a design session.
- **The same work on both sides of a comparison.** Every benchmark that solves also
  records, once per configuration, the statuses of its cases and a hash of its results
  (the bits of the result structs and moles, the `Harness` bit hash). The hashes of the
  before and after runs must be equal. The pass was bit-for-bit, so a difference is a
  finding about the code, never noise of the measurement.
- **Data come from files.** The user's state records are a data file of this node
  (`data/user-states.json`), with the pressures given as a rule: first, step, count.
  The code computes the pressure `first + i × step` by index, never by accumulation.
  Fixture-based benchmarks read `tests/Fixtures` files through `RepositoryPaths`.
- **Every group is listed by the machine.** `dotnet run -c Release --project
  tests/Benchmarks -- --list flat` prints every benchmark. The criteria below check
  against that list, not against a typed one.
- **CUDA is optional.** A CUDA benchmark on a machine without CUDA, or with
  `APTHERMO_NO_CUDA=1`, is skipped. The skip is recorded with the engine's
  `CudaSkippedBecause`; it never fails.

## Dependencies

[Problems](../../src/Problems/API.md)
[Execution](../../src/Execution/API.md)
[Thermo](../../src/Thermo/API.md)
[Performance](../../src/Performance/API.md)
[Data](../../src/Data/API.md)
[Fixtures](../Fixtures/API.md)
[Harness](../Harness/API.md)

Outside the tree: BenchmarkDotNet, pinned in `Directory.Packages.props` with the
version the node's first commit records here.

⚠ 2026-09-15, design: this list is the design's expectation. The coder trims or extends
it to exactly the nodes the code names, which the dependency check holds. Any difference
is recorded here with the reason.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Project.** `AerospacePropellantThermodynamics.Benchmarks`, an executable in the
  solution. It builds with the solution, so the protocol checks cover it. It has no
  test SDK, so `dotnet test` runs nothing of it.
  - BenchmarkDotNet requires public benchmark classes; each is named in `API.md`.
  - The root's code-shape constraint holds as for every type of the tree.
- **Configuration.** One job for every group:
  - Release, x64, the in-process toolchain, so that ILGPU's native libraries and the
    CUDA post-link load as in the library;
  - `MemoryDiagnoser` on;
  - iteration counts bounded so that a full run on the reference machine takes about
    an hour or less.

  One-time costs run as cold starts.
- **Groups.** Every group gets its own class.
  1. **Batch throughput.** The LOX/LH2 rocket sweep family at 1 000, 10 000 and 100 000
     cases, on the CPU accelerator with all cores and on CUDA, through the `Engine`
     batch API. The engine's run timings are recorded beside the means: kernel against
     upload and download.
  2. **Problem kinds on the CPU accelerator.** tp, hp, sp, and rocket in shifting and in
     frozen flow, each with and without transport, so that a regression points at a
     node.
  3. **One case through `Solver`.** The latency a .NET caller sees.
  4. **One-time costs.**
     - database load;
     - chemical-system assembly;
     - kernel compilation on the CPU accelerator, and on CUDA with the libdevice
       post-link;
     - species-table upload.
  5. **Allocations on the batch path.** The root's no-allocation-during-a-solve
     invariant gets a figure, from `MemoryDiagnoser`.
  6. **The user's state runs.** Four records of another simulation: element moles per
     kilogram and an enthalpy, AP/HTPB/Al compositions with 17.5, 20.7, 0 and 39.7 %
     Al by mass.
     - The pressures run from 1.0e6 to 6.5e6 Pa in steps of 5.0e5: 12 pressures, 48 hp
       states.
     - They go through `Solver.SolveStates` on the CPU accelerator and on CUDA: all 48
       together, and each record's 12 alone.
     - Record 4 reaches the 2700 K region of the `ALN(L)` enthalpy gap at 6.5 MPa, so
       its per-record figure is watched.
- **Results.** Text, committed.
  - BenchmarkDotNet's GitHub markdown and CSV go under
    `results/<yyyy-mm-dd>-<commit>/`, together with a `run.md` that records the
    machine, the driver, the commit, the build and what else was running (nothing).
  - A comparison goes in `results/comparison-<yyyy-mm-dd>.md`: the mean and the 99 %
    confidence interval of each benchmark on each side, and the ratio. A change whose
    intervals do not overlap is marked.
- **The before point.** The branch `bench/before-clean-code`, from `7661ea9`.
  - `7661ea9` is the first commit of the clean-code pass and changes documents only, so
    its code is `main`'s `8e36a27`.
  - The branch carries a copy of this node, written against the public subset both
    versions share. The `Engine` batch API did not change, and `Solver.Solve` and
    `Solver.SolveStates` exist in both.
  - Every adaptation is listed in that branch's commit message. The known ones are
    `Solver.Mixture` → `MixtureOf`, `CandidateSpecies` → `CandidateSpeciesFor`, and
    `Reactant.Custom` with the formula inline.
  - The branch is never merged, and kept for reproducibility.
- **Run conditions.**
  - The reference machine, a Release build, and no agent or build running in parallel.
  - After and before run back to back in one sitting with one configuration, in the
    order after, before, after, so that drift shows.

## Acceptance criteria

- [ ] The node builds in Release with the solution, and `--list flat` lists every group
      of Constraints. The protocol checks stay green: lint, and `tests/Protocol.Tests`
      (surface, coverage, declarations, dependencies, shape).
- [ ] Every solving benchmark records its statuses and results hash. On one short run of
      each group, every case is `ok`, or its status is the one its fixture expects.
- [ ] The user's state runs read `data/user-states.json`. Its four records equal the ones
      the user gave on 2026-09-14. Its 48 pressures are generated by index. Every one of
      the 48 states is accepted.
- [ ] `bench/before-clean-code` exists from `7661ea9`, builds in Release, and lists the
      same benchmarks, or names in its commit message every benchmark the old API cannot
      express.
- [ ] The comparison run: after, before, after on the reference machine with nothing
      else running.
      - The results and `run.md` of each run are committed.
      - The results hashes of before and after are equal, group by group.
      - `results/comparison-<date>.md` marks every change beyond the confidence
        intervals, and each marked change is explained or handed to a design session.

## Taboos

- No timing assertion anywhere in the tree.
- No benchmark inside `dotnet test`.
- No state record, pressure or fixture value typed into code.
- No figure recorded from a Debug build, from a machine under load, or from a run with
  another agent building.
- No change to library code for a benchmark's sake: this node measures, it does not
  tune. A finding goes to a design session.
