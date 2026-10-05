# ACCEPTANCE.md — AerospacePropellantThermodynamics (tree root)

Readiness evidence of the tree root (`AGENTS.md`, §6 and §15). The invariants and
constraints cited below as "above" or "below" are those of `BOOT.md`, which holds the
frame; this file holds the criteria that prove it, read only in the root node.

## Acceptance criteria

- [x] 2026-09-12 — For LOX/LH2, LOX/RP-1, N2O4/UDMH and AP/HTPB/Al the chamber,
      throat and exit states and the performance figures, in equilibrium and in frozen
      flow, agree with the NASA CEA reference outputs within the tolerance table of the
      fixtures node. The list of reference files is produced by a directory listing,
      and every file in it is covered:
      `Problems.Tests.RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
      over every file of `tests/Fixtures/cases/rocket` (89 that day: the four
      propellants with and without transport, and the RP-1311 rocket examples) and
      `EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`,
      `AssignedEnthalpyCasesReproduceTheReference`,
      `AssignedEntropyCasesReproduceTheReference` over every tp, hp and sp file
      (106). 2026-09-13: 98 rocket and 115 equilibrium files after the
      melting-plateau cases (example 13 and the plateau band), the same tests green.
      The documented defects of the reference (the fixtures node's BOOT.md: the
      reacting conductivity where a trace component is eliminated, the
      frozen-station cv, the singular derivative matrix of a bound-exact tp) are
      skipped by the rules recorded there, and each skip is guarded: the defect must
      be visible on the reference's own output.
- [x] 2026-10-05 — A batch of 100 000 states on CUDA equals the same batch on the CPU
      accelerator bit for bit, statuses and iteration counts included, a NaN's payload
      aside (the GPU-equals-CPU invariant), and two runs of it on CUDA are identical
      (`CudaTests.TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic`
      in the execution tests node, long-running, through its `ExactComparison`). Evidence:
      the execution node's criterion of 2026-10-05 (`src/Execution/ACCEPTANCE.md`):
      `Category=Cuda` 80 of 81 on Windows in Debug, the one other the throughput fact,
      which refuses a Debug run by design, and 81 of 81 under WSL2 in Release.

      ⚠ 2026-10-05: was "within the tolerance table", ticked 2026-09-12 and re-verified
      2026-09-15 at `62cd99e` on the reference machine with
      `CudaTests.TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`,
      the list of compared fields produced by reflection over the result type and the
      table's second tier for mole fractions described under the invariant
      (HISTORY.md#gpu-tier). The tree's own math made the comparison exact, and the
      tolerance table is gone (HISTORY.md#gpu-exact).
- [x] 2026-09-12 — On the reference machine the CUDA path is at least 5× faster than
      the CPU accelerator path with all cores on the 100 000-state batch; the measured
      figure is recorded in the benchmark's approved file
      (`tests/Execution.Tests/Throughput.approved.txt`: 56.28×, CUDA 0.170 s against
      9.544 s; `CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio`).
      Re-verified 2026-09-15 on the decomposed code at `62cd99e`, same test,
      `Throughput.approved.txt` unchanged. Re-measured 2026-09-19 in Release, the
      configuration the release runs, as the median of three runs of the release job's
      filter: 23.58× on Windows (CUDA 0.151 s against 3.557 s) and 27.48× under WSL2
      (0.204 s against 5.593 s), recorded in `Throughput.approved.txt` and
      `Throughput.linux.approved.txt` with their configuration (merged as `a316ecb`).
      Re-measured 2026-10-05 after the tree's own math (Release, the median of three
      runs): 20.07× on Windows and 31.07× under WSL2, both files re-approved; the CUDA
      kernel 1.43e-7 s per Newton step against 7.85e-8 s before, the CPU accelerator
      7.2 s against 5.3 s for the batch, measured on a machine shared with other test runs
      (the execution node's criterion of that date, which asks for a quiet re-measure).

      ⚠ 2026-09-19: the figures above of 2026-09-12 (56.28×) and the Linux 52.01× were
      Debug measurements, a fact no record stated. The CPU accelerator runs the kernels
      from the assemblies' IL, so the host build configuration changes its speed about
      2.8× (9.5 s in Debug, 3.4 s in Release), while the CUDA kernel does not depend on
      it. The release rehearsal of 2026-09-19 compared a Release run with the Debug
      record and failed at 29.48×. Found by a Fable 5.1 analysis that ruled out the
      toolkit, the driver, the clocks and the code. The 5× target holds by a wide
      margin in both configurations; the tripwire now records and asserts its
      configuration, and its floors are unchanged.
- [x] 2026-09-12 — The full test suite passes in a process where CUDA is forbidden
      (environment variable `APTHERMO_NO_CUDA=1`, honoured by the execution node):
      `dotnet test AerospacePropellantThermodynamics.sln` with the variable set, 1742
      tests green after the protocol tests node (1733 after the Cli node, 1654 after
      the Problems node, 850 after the Execution node; 1749 on 2026-09-13 after the
      front door's mass check and 1951 after its declared tolerance and mass report,
      the `Problems` BOOT.md; 2143 on 2026-09-14 after the melting-plateau rule, the
      `Equilibrium` BOOT.md; 3037 on 2026-09-15 after the clean-code pass, at
      `62cd99e`), none skipped, the CUDA-category tests verifying the refusal instead.
- [x] 2026-09-12 — The tree passes `protocol_lint` without errors (the lint command
      of `CLAUDE.md`, run after every node and by `Protocol.Tests.LintTests` in
      every test run, last after the protocol tests node: 0 errors,
      0 warnings).
- [x] 2026-09-13 — The reflection checks are written for this stack and each was
      shown red once (AGENTS.md §13): the protocol tests node
      (`tests/Protocol.Tests`: `SurfaceTests`, `CoverageTests`, `DeclarationTests`,
      `DependencyTests`, with `LintTests` running the linter and `InvariantTests`
      holding the three root invariants above), ten mutations applied alone and seen
      red, listed in that node's `BOOT.md`; the surface snapshot is
      `tests/Protocol.Tests/PublicSurface.approved.txt`. The first run over the tree
      found one undocumented public type (`Execution`'s `SpeciesFunctionBatchViews`,
      fixed in its `API.md`).
- [x] 2026-09-15 — The tree meets the code-shape constraint above: no type over 400
      lines of code, no method over 60, no control flow nested deeper than 3, no
      method with more than 6 parameters, no `src` type with Ce over 14 outside the
      registries and composition roots the nodes declare, every stable `src` type in
      shape, no dependency against instability; measured by the protocol tests node's
      `ShapeTests`, all ten facts green at `62cd99e`, over a machine-generated list of
      every type and method of every assembly, the declared exceptions read from the
      nodes' `BOOT.md`. The review of 2026-09-14 (nine read-only reviews over the tree
      at `8e36a27`, one per node group and one across the boundaries, counting
      physical lines) found 5 types over 400 lines (`EquilibriumSolver` 1289,
      `TransportSolver` 794, `Problems.Solver` 663, `Engine` 509,
      `Protocol.Tests.Tree` 428), 31 methods over 60 lines (the longest
      `TransportSolver.Evaluate` 640 and `EquilibriumSolver.Solve` 512) and 10 types
      with Ce over 10 by its textual count; the decompositions are designed in the
      nodes' `BOOT.md` files under `## Structure` and each is accepted only with its
      node's bit-for-bit or field-by-field guard green.

- [x] 2026-09-17 — Linux x64 (2026-09-15): the fast suite is green on the CPU accelerator, and
      the execution tests node is green on CUDA, its long-running sweep included, under
      WSL2 on the reference machine. The outcome for the bit snapshots is recorded under
      the platform constraint above.

      Evidence: WSL2 Ubuntu 24.04, .NET SDK 10.0.112, CUDA 12.9 libnvvm, at `0c3b455`
      (merged as `3bc4039`): `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 3098/3098 against the `Bits.linux.approved.txt` files, and
      `dotnet test tests/Execution.Tests` 55/55 on CUDA, the 100 000-case sweep included,
      with the throughput ratio 52.01× recorded in `Throughput.linux.approved.txt` (the
      execution tests node's `BOOT.md` explains the per-platform file). The first run at
      `f67b1a9` failed only the bit snapshots and two platform assumptions of the tests,
      the discovery test's `.dll` suffix and the Windows throughput figure.
- [x] 2026-09-18 — The packages (2026-09-15): packed by the CI from a commit, `APThermo` restores
      from a local feed into every sample, and each sample reproduces its approved
      output on Windows and on Linux. `APThermo.Cli` installs from the same feed as a
      .NET tool and runs an approved example without `--database`. Every source
      document of every packed assembly resolves through SourceLink to the commit's
      file on the public repository. Before the first release the package READMEs link
      the guide on the public repository; until it exists they carry no guide link.

      Evidence: CI run 35274736153 of `24cd966`, green on `windows-latest` and
      `ubuntu-latest`, whose steps pack both packages, restore `APThermo` from the
      job-local feed into all twelve samples and diff each output with its approved
      file, and install the tool from that feed and run its approved example. The
      symbols: `sourcelink test` passed on all 16 PDBs of `APThermo.snupkg` and
      `APThermo.Cli.snupkg` packed from `8d0b7a1`, and the sampled
      `raw.githubusercontent.com/baryon-asymm/APThermo/<commit>/…` URLs answered 200
      (`SCRATCH/sourcelink-proof.txt`, kept out of the tree with the audit reports).
      The package READMEs link the guide since `20cc262`.

      ⚠ 2026-09-18: the criterion said "a debugger steps from a sample into the
      library's source through SourceLink, and the step is recorded". A step in an IDE
      is a human action that no run can repeat, so the record would age into a claim
      nobody re-checks. The wording now names what a debugger actually needs and what
      a machine can re-prove: every document of every packed PDB resolves to the
      commit's file. The owner may still step through it by hand; nothing in the tree
      depends on that.

      ⚠ 2026-09-17: restored after an unreviewed rewrite of 2026-09-16 that dropped the
      package restore into the samples (the ⚠ of that date, now at
      [HISTORY.md#restored](HISTORY.md#restored)).
- [x] 2026-09-17 — The documentation (2026-09-15), proven by the docs tests node, with every check
      shown red once and failing on an empty set:
      - every C# block of the guide equals its snippet (`SnippetTests`, and
        `FenceTagTests` for the fences' tags);
      - every `apthermo` invocation shown is run and its output approved, except the
        declared synopses whose output depends on the machine or the release
        (`CommandLineExampleTests.EveryCommandLineInvocationIsACheckedExampleOrADeclaredSynopsis`);
      - every sample prints its approved output
        (`SampleOutputTests.TheScenarioPrintsItsApprovedOutput`, `ScenarioTableTests`);
      - every link resolves (`LinkTests`);
      - every shown or sample document validates against its schema
        (`SchemaValidationTests`, `CliDocumentTests`);
      - every guide page has the shared shape (`GuideShapeTests`).

      Evidence: `tests/Docs.Tests` 28/28 green at `587f05d` on Windows (27/27 at
      `f67b1a9` under WSL2), the red-once and empty-set records in that node's
      `BOOT.md`, and four read-only documentation reviews on 2026-09-17, the last at
      `48fecae` with no blocker and no major; its minors were closed at `587f05d`.

      Corrected 2026-09-17: the list follows the check table of
      `tests/Docs.Tests/BOOT.md`, where the Documentation bullet of `## Delivery` points.
      "Every code block … equals its sample region" predated the snippet markers and
      named only four of the six proofs.
- [x] 2026-09-25 — Diagnostics (2026-09-24): the tree builds at the maximum of the Diagnostics
      constraint with 0 warnings and 0 errors, and nothing suppresses a diagnostic.
      - `DiagnosticsTests` is green, each of its facts shown red once and failing on an
        empty set.
      - The bit snapshots are unchanged, and the fast suite and the protocol lint are
        green.
      - The execution tests node is green on CUDA on the reference machine, its
        long-running sweep included, because the result structs changed shape.
      - The public surface snapshot moves only by the structs' properties and equality,
        the standard exception constructors (the four library exceptions and the
        fixtures node's `FixtureFormatException`), the harness's
        `FixtureFamilies.Of` split into `Keys` and `CasesOf` with its `FixtureFamilyKey`,
        and the benchmarks node's public `BenchmarkEnvironment` with the new, empty
        `APThermo.Benchmarks.Runner` section.

      Evidence at `b8cde93`, on the reference machine (Windows), from a tree with every
      `bin` and `obj` removed:
      - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
        "Category!=LongRunning"`: 3108 of 3108, none skipped, `DiagnosticsTests`
        included (its red-once records are in
        [tests/Protocol.Tests/HISTORY.md#diagnostics-red-once](tests/Protocol.Tests/HISTORY.md#diagnostics-red-once));
      - `dotnet test tests/Execution.Tests -c Release`: 56 of 56 on CUDA, the
        100 000-case sweep and the throughput tripwire included;
      - no `Bits*.approved.txt` or `Throughput*.approved.txt` differs from `main`
        (`0899500`);
      - the protocol lint: 0 errors, 0 warnings.

      Linux, and a second Windows machine: CI run 36229326350 of `a94a108` (2026-09-26)
      is green on `windows-latest` and `ubuntu-latest`, fast suite without the bit
      snapshots, packing, samples against the package and the packed tool's example
      included. The two runs before it failed on two gaps this phase opened outside
      the tree's own build:
      - `DiagnosticsTests` read the NuGet cache CI keeps inside the workspace, fixed in
        the protocol tests node (the ⚠ of 2026-09-26 there);
      - the workflow's scratch program compiled a public Harness type into an
        executable (CA1515), fixed in `.github/workflows/ci.yml`.

      ⚠ 2026-09-25: the surface sentence named only the structs and the four library
      exceptions. Fixing the test nodes and the benchmarks node moved three more entries
      for the same constraint (CA1032, xUnit1042/1045, and CA1515 with the owner's
      split of the benchmarks node). Found by the coder of the last step, who raised it
      rather than editing the root.
- [x] 2026-09-26 — Every GPU architecture (2026-09-26): the CUDA path runs on every architecture
      ILGPU 1.5.3 declares from compute capability 7.5 up, and an engine that binds
      CUDA has loaded a kernel carrying every wrapper of the math list. The list of
      architectures comes from ILGPU by reflection. The evidence is the execution
      node's criterion of the same date. Until it is ticked, 0.1.0 throws on every CUDA
      run of every GPU older than Blackwell, and `CHANGELOG.md` says so under 0.2.0.

      Evidence: merged as `f973870`, on the reference machine. The architecture fact
      covers the 11 architectures SM_75 … SM_121 and the 5 entry points; both paths of
      the post-link occur; the PTX is equal and the probe bits are identical. The
      bind-time facts are green, and `tests/Execution.Tests` in Release is 134 of 134.
      Every architecture is compiled for and then run on the RTX 5070 Ti; no GPU older
      than Blackwell has run it. A one-time run on rented hardware (a T4 or an L4) is
      planned by the owner and will be recorded here.

      ⚠ 2026-10-05: was "has loaded a kernel carrying every wrapper of the math list", now
      no kernel calls a libdevice function and there is no wrapper to carry
      (HISTORY.md#ilgpu-libdevice-retired). The architecture fact holds the new post-link
      instead: for every architecture from SM_75 up, the kernels pass it, equal the
      device's own in PTX and give the probe's bits
      (`ArchitectureTests.EveryArchitectureFromSm75UpPassesThePostLinkAndMatchesTheDevice`,
      the execution node's criterion of 2026-10-05).
- [x] 2026-09-30 — The second hidden-defect audit (2026-09-28, five read-only parts at `5a732f0`,
      reports kept out of the tree with the first audit's) is closed before 0.2.0 is
      tagged: every finding of every part is fixed or answered by an owner's decision,
      recorded in the node it concerns. The owner decided on 2026-09-28:
      - everything is fixed before the tag, minor findings and guard gaps included;
      - the throat is the first maximum of the mass flux met from the chamber (the
        performance node's `BOOT.md`);
      - below 200 K the equilibrium node follows the reference's mixture window;
      - the retention threshold has the reference's two stages.

      Two findings were regressions of the first audit's own fixes (the reaction basis
      of `Transport`, the loop rules of `Equilibrium`). Every fix is therefore accepted
      only with the audit's own failing cases as facts, and a short third pass over the
      changed code follows before the rehearsal.

      The third pass (2026-09-28, two read-only parts at `c02e14d`, reports kept with
      the others) found one more regression of a fix: the plateau-edge rule of
      `Performance` turned Li/O/H bands that were `Ok` into `ThroatNotFound`. Its
      findings are designed in the nodes they concern (`Performance`, `Equilibrium`,
      `Execution`, `Problems`, the protocol tests and fixtures nodes, and the rule-set
      line of the Diagnostics constraint above) and close before the tag like the rest.

      Evidence, on the reference machine, at `9284418` and after: every node's criteria of
      the audit and of the third pass ticked in the node they concern, the three that waited
      for a run on GitHub (the fixtures node's binding step, its sample and its platform, and
      the harness's field comparison) on CI run 36734450932 of `71e389c` (2026-09-30), green
      on `windows-latest` and `ubuntu-latest`. The whole solution builds with 0 warnings and 0 errors, the protocol lint gives 0 and
      0, the fast suite is green on Windows and under WSL2 with each platform's records
      recorded, and the CUDA path is green on both (the criterion below). Three
      classes of equilibrium failure are known and left to 0.2.1 by the owner's decision
      (`CHANGELOG.md`, Known limitations).

- [x] 2026-09-30 — The rocket kernel's compile is bounded (2026-09-30, the memory investigation of
      2026-09-29): the attribute of the Compile size constraint, a guard in the execution
      tests node, the engine releasing its kernels at `Dispose`, and no test allocating
      what it only measures. Evidence per the execution node's criterion of that date;
      the CUDA path is proved on the reference machine before the tag: GPU equals CPU
      (the 100 000-case sweep, every architecture) and the throughput ratio not below
      its approved floor. If the call the attribute leaves in the PTX breaks either, the
      fallback is one call site, a new design session.

      Evidence at `9284418`, the reference machine, Release: the guard, the release of
      the kernels at dispose and the bound check are in the execution node; the attribute
      is in the performance node. `dotnet test tests/Execution.Tests -c Release` on CUDA:
      171 of 171 on Windows in 2 minutes 36 seconds (37 minutes before, mostly compile),
      170 of 170 under WSL2 in 2 minutes 48 seconds, the 100 000-case sweep and every
      architecture included, so GPU equals CPU with the call left in the PTX. The
      throughput tripwire held: the ratio 24.24 against the record's 23.58 on Windows
      and 38.01 against 27.48 under WSL2, the floor being 5. The attribute's cost was
      measured by three runs of the tripwire before (`00dac9b`) and after: the CUDA
      kernel 0.150 s before and 0.157 to 0.172 s after, about 10 % slower, the CPU
      within the noise (4.65 to 4.88 s before, 4.77 to 5.01 s after). No throughput
      record is re-approved: the run-to-run spread is larger than the effect. The
      release job's filter is green on Windows in Release. The per-project peaks of the
      fast suite went from 14 to over 27 GB to 0.75 to 1.6 GB, the whole solution 4.8 GB.

- [x] 2026-10-01 — Host transfers are pinned (2026-10-01, the fourth hazard under the ILGPU
      constraint): the `Chunks` node's criterion of the same date is ticked, its stress
      among its evidence, before `v0.2.0` is tagged again. Ticked there at `93f29c9` and
      after: CUDA on Windows and under WSL2, and 12 000 solves of the stress with no lost
      download. The tag `v0.2.0` of
      `05e2d39` is moved once, to the commit of this fix, after a green rehearsal, as
      `v0.1.0` was: its release run failed before any package was published (the
      owner's decision of 2026-10-01).

- [x] The elementary functions are one program too (the owner's task of 2026-10-02,
      item 13 of 0.2.2 since 2026-10-05): `Exp`, `Log` and `Pow` of the numerical nodes
      are the tree's own kernel-compatible C#, correctly rounded (in the manner of
      CORE-MATH or CRlibm), and the CPU accelerator and CUDA run the same code instead of
      the C runtime and libdevice, with floating-point contraction into FMA held equal on
      both sides. Why: the two implementations differ by up to 3 ULP (the execution tests'
      probe kernel), so a stop test near the noise flips between accelerators (the throat
      of the cited HTPB, 2026-10-02, `src/Performance/BOOT.md`), and the C runtime's FMA3
      dispatch makes the bit records a property of the machine (the declared deviation
      under `## Constraints`, Platform, which this lifts). Done when the GPU/CPU comparison
      of the execution tests is exact on every fixture and the sweep, the bit records
      equal on Windows and Linux and on the hosted runners, every CEA tolerance test
      green, and the throughput not below the root's 5×. A root decision: it replaces the
      math list of `## Constraints` and the execution node's libdevice wrappers.

      Evidence so far, 2026-10-05, on the reference machine:
      - correctly rounded: the thermo node's `Elementary` child, 0 wrong results over the
        full CORE-MATH worst-case lists, 2 266 122 inputs (`FullWorstCaseListsTests`), the
        fast paths' errors 6.96 (exp), 10.87 (log) and 8.94 (pow) times below their bounds
        (`FastPathMarginTests`; `src/Thermo/Elementary/BOOT.md`, its criteria);
      - exact GPU/CPU comparison on every family, the probe and the sweep, and no
        libdevice, libnvvm or CUDA Toolkit loaded (`src/Execution/ACCEPTANCE.md`, the
        criteria of 2026-10-05; the criterion on the 100 000-case batch above);
      - one bit record per node, the Windows and WSL2 bits equal
        (`tests/Harness/BOOT.md`, "One bit record per node"), and one approved document
        per command-line example (`tests/Docs.Tests/BOOT.md`, its criterion of the date);
      - the throughput 20.07× on Windows and 31.07× under WSL2 (the criterion above).

      Closed 2026-10-05: hosted CI run 37331221583 at `ed0c9ef9`, green on `windows-latest`
      and `ubuntu-latest` with the bit facts unfiltered (`ci.yml`, filter `Category!=LongRunning`);
      the whole fast set, the CEA tolerance tests in it, green on the merged tree on Windows
      (6 900 facts, 1 min 56 s) and under WSL2 (the same counts, 127 s), and again 6 921 on
      Windows in the merge guard of `ed0c9ef9`; the throughput re-approved from five quiet runs
      of the median rule, 18.52× on Windows and 25.92× under WSL2
      (`tests/Execution.Tests/ACCEPTANCE.md`, the criterion of the date).

      ⚠ 2026-10-05: was `Exp`, `Log`, `Log10` and `Pow`, "not scheduled"; `Log10` left the
      math list instead, no numerical node needing it (HISTORY.md#allow-list-own-math).
- [x] The test pyramid (2026-10-03, `## Constraints`, Test time budgets): the fast set and
      the end-to-end set within their budgets on the reference machine, on Windows and in
      WSL2, both `dotnet test` durations recorded here; every fact that starts a process
      carries `Category=EndToEnd` (a Protocol.Tests check, seen red once); the approved
      outputs and bit records proven through the real process.
      2026-10-05, the merged own-math tree (Debug, `APTHERMO_NO_CUDA=1`): Windows fast set
      1 min 56 s (budget 5), end-to-end set 2 min 04 s (budget 10); WSL2 127 s and 135 s;
      `Protocol.Tests` `EndToEndTests.EveryFactThatStartsAProcessCarriesEndToEnd` (seen red
      in the pyramid's merge, 2026-10-03); the process facts of `Cli.Tests` and `Docs.Tests`
      prove the approved outputs and bit records through the real process, green on both.
