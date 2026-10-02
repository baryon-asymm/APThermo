# ACCEPTANCE.md — Protocol.Tests

The acceptance criteria of `tests/Protocol.Tests`, moved here verbatim from its `BOOT.md`
on 2026-10-02 (AGENTS.md 3.2, §6 and §15: the leaf stood at 395 of its 400 lines). Read
only in this node.

## Acceptance criteria

- [x] 2026-09-13 — Lint wired:
      `LintTests.TheTreePassesTheProtocolLinterWithNoErrorAndNoWarning` runs the linter as a
      process in strict mode; red once on mutations 5, 3a and 10 → HISTORY.md#crit-lint
- [x] 2026-09-13 — Surface, Coverage, Declarations, Dependencies and the root invariants
      green on the tree: `SurfaceTests`, `CoverageTests` (two facts), `DeclarationTests`,
      `DependencyTests`, `InvariantTests` (single precision, CUDA names, static fields);
      the first run's three findings → HISTORY.md#crit-green
- [x] 2026-09-13 — Each check shown red once, ten mutations (1) to (10) applied alone and
      restored (`mutations_protocol.sh`), among them (3a) a missing and (3b) an unused
      dependency and (4) an unsnapshotted public constant → HISTORY.md#crit-ten-mutations
- [x] 2026-09-14 — The support code in shape (`## Structure`, F-TF-02, F-TF-08, F-TF-17):
      `Tree` split, `ApiDeclarations` read by two checks, every fact one loop and one
      assertion; `PublicSurface.approved.txt` unchanged, the ten mutations red again
      → HISTORY.md#crit-support-code

  ⚠ 2026-09-14: was a member "fixed upstream at `431e684`", now by the merge `8f051d8`
  → HISTORY.md#crit-support-code-fix
- [x] 2026-09-14 — The two uncovered diagnostics seen red (F-TF-15): (3c) a link to a
      descendant, (3d) a link to an `API.md` no node has → HISTORY.md#crit-3c-3d
- [x] 2026-09-15 — Shape level green: every fact of `ShapeTests` (the test runner lists
      them: the five over-limit rules, stable type, stable dependencies, mechanics, named
      construction, the reverse row fact) over the types and methods the check enumerates
      itself, each seen red once, the three `src` rules through synthetic inputs
      → HISTORY.md#crit-shape-level
- [x] 2026-09-15 — The Shape level's non-degeneracy gaps closed (R-Protocol.Tests-14):
      every Shape fact asserts a non-empty input, each guard seen red once
      → HISTORY.md#crit-shape-gaps
- [x] 2026-09-15 — R-Protocol.Tests-12: each `src`-only rule seen red on a reverted
      mutation of a real `src` node → HISTORY.md#crit-r12
- [x] 2026-09-15 — R-Protocol.Tests-13: eight branches the first proofs never exercised
      each seen red once → HISTORY.md#crit-r13
- [x] 2026-09-15 — Child nodes: every check reads one attribution (`NodeOf(Type)`); four
      proofs on a temporary child directory seen red → HISTORY.md#crit-child-nodes
- [x] 2026-09-15 (child-nodes phase) — The stable-dependencies measure counts `src`
      nodes holding a project (`CouplingMeasures.NodeCoupling`), the fold seen red
      → HISTORY.md#crit-stable-deps
- [x] 2026-09-15 (distribution phase) — Tree contract level wired: the four facts of
      `## Tree contract` (`TreeContractTests`) green on the tree without a document
      outside this node touched, each seen red once → HISTORY.md#crit-tree-contract
- [x] 2026-09-25 — Diagnostics level: the five facts of `## Diagnostics check` green,
      each failing on an empty set and seen red once → HISTORY.md#crit-diagnostics-level
- [x] 2026-09-26 — The Diagnostics walk skips every dot directory but `.github` (the ⚠
      of that date), both scope mutations seen as stated; CI run 36229326350 of
      `a94a108` green → HISTORY.md#crit-dot-walk
- [x] 2026-09-27 — The blind spots of the guards audit (Constraints), F2, F3, F4, F8, F9:
      `NoSourceFileCarriesAGeneratedCodeMarker`, `NumericalNodesHaveNoMutableStaticField`,
      `NumericalNodeSourcesNameNoSinglePrecisionSyntax`,
      `TreeContractSnapshotTests.TheTreeContractSignaturesMatchTheApprovedSnapshot`, each
      mutation seen red once, no `Bits*` or `Throughput*` record moved
      → HISTORY.md#crit-blind-spots
- [x] 2026-09-27 — No `Math.Min` or `Math.Max` in the numerical nodes:
      `InvariantTests.NumericalNodesCallNoMathMinOrMaxAndNoNanOrNegativeCheckOutsideKernelMath`,
      seen red two ways → HISTORY.md#crit-min-max

  ⚠ 2026-09-28: was a fact matching `System.Math` and two names, now an allow-list
  → HISTORY.md#crit-min-max-superseded
- [x] 2026-09-28 — The audit fixes of 2026-09-28, each fact seen red once by the audit's
      probe: `NumericalNodesCallOnlyTheAllowedMathAndDoubleMembers`,
      `NoSourceFileCarriesAGeneratedCodeMarker`, `NoBuildFileSuppressesOrOverridesADiagnostic`,
      `KernelReachedMethodsAllocateNothingAndThrowNothing`,
      `EveryCompiledSourceLivesInANodeDirectory`; 35/35 at `7da07dc` → HISTORY.md#crit-audit-fixes
- [x] 2026-09-29 — The third audit pass (4a, 4b): the integer constant on the left,
      `NumericalNodeSourcesPutNoConstantLeftOfAnOrderedFloatingComparison`, and the rule
      sets, `NoBuildFileSuppressesOrOverridesADiagnostic`, each seen red on the audit's
      own mutation; 35/35 at `0008a45` → HISTORY.md#crit-third-pass
- [x] 2026-09-30 — The compiled-source walk skips restored NuGet packages' sources:
      `CompiledSourcesTests.OnlyASourceBelowAPackageFolderIsThePackages`; 36 of 36 on
      Windows and WSL2 with the cache inside the tree → HISTORY.md#crit-package-sources

  ⚠ 2026-09-30: was the in-tree cache `.nuget-packages` "git-ignored", now it is not
  → HISTORY.md#crit-package-gitignore
- [x] 2026-10-01 — No `src` method passes host memory to ILGPU by reference (the root's
      fourth ILGPU hazard):
      `InvariantTests.NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference`, green at
      the fix (37 of 37), red once on the unfixed `ChunkBuffer.cs` and on an empty walk
      → HISTORY.md#crit-ilgpu-byref

  ⚠ 2026-10-01: was "a by-reference parameter", now one whose element type is not
  `Span<T>` or `ReadOnlySpan<T>` → HISTORY.md#crit-ilgpu-byref-wording
- [x] 2026-10-02: a declaration under ✅ whose return type is a tuple (`internal static (long Low,
      long High) Band(...)`) is read as a member named `Band`, not `static`: no `API.md`
      of the tree holds such a line today, so the false failure is latent (reported
      2026-10-02 by the boot-api-protocol skill's reference builder, which fixed its copy).
      `ApiDeclarationsTests.ATupleReturningDeclarationIsReadByItsMemberName` parses it, named and unnamed tuples, and names `Band`; red 3 of 3 on the old parser.
      2026-10-02: the same declaration split across lines, the tuple closing on the line of the name
      with the parameters on the next (`internal static (long Low,` / `long High) Band(` /
      `double p = 0.5);`), was read as `Band` plus a field `p` (the tuple's `)` cancelled the name's
      `(`); `ApiDeclarationsTests.ADeclarationSplitAcrossLinesIsReadByItsMemberName` was red on the
      parser as it stood (`["Band", "p", "Next"]`) and is green after the parameters are collected from
      the name's own parenthesis; no existing declaration of any `API.md` changed its reading
      (`DeclarationTests` green).
- [x] 2026-10-02 — Every `tools/*/test_*.py` of the tree runs green inside the fast set, one
      case per script found by the walk (`ToolSelfTestTests.EverySelfTestExitsZero`: the
      three of 2026-10-02, the linter's, the merge guard's and the coder-scope hook's, named
      in its output), an empty walk failing (`TheWalkFindsTheSelfTests`); the process code is
      `PythonProcess`, shared with `LintTests`. Red once: a scratch copy of the coder-scope
      self-test with one failing case, uncommitted, turned the case
      `EverySelfTestExitsZero(script: "tools/coder-scope/test_zz_scratch.py")` red with the
      script's output tail, the other four green; the walk's pattern changed to match nothing
      turned `TheWalkFindsTheSelfTests` red (`Collection was empty`). Cases of the class run
      one after another, so the self-tests' temp directories never meet (no collection
      attribute was needed); the merge guard's takes about 70 s, the slowest case of the node.
