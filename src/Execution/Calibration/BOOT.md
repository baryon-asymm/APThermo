# BOOT.md — Execution.Calibration

## Purpose

Bounded empirical launch calibration for an identical repeated workload. A cold
bounded run still starts with one case and grows by at most twice the preceding
launch. A repeat may probe a previously observed initial prefix. This reduces
repeated underoccupancy while preserving every calculation and original gate.
This project-less child belongs to the Execution assembly and has no CUDA type.

## Invariants

- One cache entry per session; constant retained space. Hold only digests, weak
  launcher/table identities, scalar/plan metadata and prefix/time observations.
  Never retain batch arrays, buffers, results, delegates or tables strongly.
- ReusedPrefix describes the latest first-launch freshness decision. Parent FirstLaunchCases/ReusedPrefix describe only the first current successful synchronized launch (zero/false when absent). Never cache a numerical result or RunTimings. Every call performs all its work,
  uploads and downloads and records current successful synchronized launches.
- Learn only an actually observed contiguous prefix beginning at case zero, at a
  full prior launch boundary, whose case count is at most static plan.Size and
  cumulative synchronized duration is at most the unchanged launch target.
  No rounding to a larger prefix or substitution of a fast tail chunk.
- A merged prefix has not itself been measured before its first probe. Historical
  prefix cost is an empirical sampling rationale, not a duration upper bound.
  Keep the 500 ms target, quarter-of-2-second headroom and timeout/lost handling.
- All later growth remains at most twice the preceding physical launch. Every
  chosen length respects current count, static size, memory and offset caps.

## Dependencies

[Execution](../API.md)
[Chunks](../Chunks/API.md)

Outside the tree: .NET 10 TimeProvider for a monotonic clock, reflection and
SHA-256; ILGPU 1.5.3 ArrayView type identification only, without CUDA runtime.

## Constraints

- R10 observational stage: retain one immutable scalar/value-copy observation of
  the latest transaction, with bounded sequence identity. Record prior-entry and
  publication status, the actual single identity comparison reason, prior prefix,
  and the current generation separately from normal consume-at-Begin. Never infer
  external invalidation solely from Begin's own Clear/generation increment.
- Record first-choice decisions before and after initial upload, with nullable
  candidate ages from the SAME existing monotonic clock. Preserve the specific
  expired/mismatch reason after candidate removal; the second check must not erase
  it as generic absence. Freeze the selected physical first-launch decision when
  that current launch succeeds. Publication/abandonment is a separate outcome.
- Publish diagnostic completion only at the existing post-result-assembly boundary.
  Keep RunTimings unchanged: it is assembled before Complete. Parent/friend tests
  read a value copy after Run returns. No retained table/delegate/array/buffer/result,
  cached timing, growing log or callback; expose no new package API or global hook.
- Begin captures the prior transaction's terminal outcome before its normal Clear,
  including when no entry exists. Completion distinguishes unsupported identity,
  incomplete coverage, invalid/over-target observation, no useful prefix, publication,
  abandonment and superseding generation. A generic NotPublished alone cannot tell
  why the next call found no reusable observation. Use the original decision state;
  do not recompute work, change branch ordering or publish before result assembly.
- This stage changes NO matching order, launch choice, target, TTL, growth, median,
  workloads, input hashing or failure/lifecycle ordering. Reuse existing clock reads
  where possible. Observation has finite cost and is not a performance correction.
  CPU/unbounded/nonapplicable state must not masquerade as a fresh CUDA transaction.

⚠ 2026-11-15 (R10): actual sequential source463c passes Windows but fails Linux's
throughput guard with cold/cold/hit choices. Bool-only reuse data cannot identify
expiry, incompatibility, absence or failed publication. Capture those decisions
before changing policy; the exact historical miss cause remains unknown.

- The input digest is captured from ChunkBuffers BEFORE Allocate: Constant is
  uploaded there. Inputs stay stable until Run ends; caller mutation during a run
  is unsupported, as concurrent use of an engine already is. InputOutput is keyed
  from its initial seed bytes, never the results written back by DownloadChunk.
- The declarative digest frames every buffer's order, direction, unmanaged type,
  host length and per-case stride; all Input/Constant/InputOutput bytes participate.
  Output/scratch contents do not; their structural metadata does. SHA-256 equality
  is practical identity under its collision assumption, not a collision-free proof.
- Capture every actual non-ArrayView descriptor field automatically, including
  Seeded and ExitCount, with field names/types/values. Current supported scalars are
  Int32. Unsupported scalar or view shapes decline reuse, rather than omit a field.
- Key identity also includes actual cached launcher and UploadedTables via weak
  ReferenceEquals, view type, plan.Count, plan.Size and budget target. The input
  digest includes the current byte/element bounds' declarations.
- Consume the previous entry before matching or reuse. A miss, expired/dead weak
  target, changed key, unsupported input, failed/partial run, cleared/recompiled
  kernel, disposed/replaced table, lost session or engine disposal clears it.
- An entry expires after one current target interval since successful completion,
  using monotonic time; check freshness again at the first launch. Backward time
  is not fresh. This narrows the empirical reuse window, not a timing guarantee.
- Keep a pending observation during execution; publish only after all uploads,
  synchronized launches, downloads, primary-aware cleanup and result construction
  succeed. Any actual launch above target prevents publication. A one-case prefix
  has no useful reuse. Clear on all failure paths without replacing the primary.
- CPU and unbounded plans bypass calibration and hashing. Generic batch-loop calls
  without a complete identity take the cold path. No benchmark-only special case.
- Use a disposable run transaction. Begin/identity failures after device allocation
  must enter primary-aware cleanup. Failure before Begin must clear the old entry.
  Completion updates a prepared compact entry, not an allocation of retained inputs.
- The parent owns pipeline/session lifecycle wiring; Chunks owns byte framing.
  This node owns identity matching, weak lifecycle and prefix observation only.
  Root code-shape limits apply without a new broad exception.

## Acceptance criteria

- [x] Cold and every incompatible workload start at one; a compatible live repeat
  reuses only the observed prefix, then retains current 2x and all static bounds.
  (2026-11-15: `CalibrationIdentityTests`, `CalibrationPrefixTests` and
  `CalibrationLoopTests` pass in both source-pinned `c0df7b146ffd` CPU cohorts.
  Controlled first-prefix histories 15 and non-doubling 10 exercise actual BatchRun.
  R12 evidence: external `fix-evidence/worker-calibration-loop-R12-SCRATCH/`.
  Static memory/offset bounds and subsequent 2x growth remain unchanged.)
- [x] Machine-enumerated buffer mutations, seed A→output B→new input B, scalar,
  view type, table, launcher, plan and budget changes cause misses; clone A may hit.
  (2026-11-15: `BufferIdentityTests`, `CalibrationIdentityTests` (including
  `ProductionDescriptorsIncludeEveryActualIntegerScalar`) and
  `InitialSeedIdentityIsCapturedBeforeDownloadMutatesIt` pass in both c0df CPU cohorts.
  Initial digest A, changed output/input B, clone A and scalar/plan/table/launcher
  controls remain distinct.  R12 stale/post-download key mutants fail and are restored.)
- [x] Prefixes never come from tail timing, rounding, partial completion or an
  over-target launch; expiry before match/launch and backward clock take cold path.
  (2026-11-15: `CalibrationPrefixTests` and
  `CalibrationLifecycleTests.ExpiryDuringUploadRestoresTheColdPhysicalLaunch`
  pass in both c0df CPU cohorts. Fast-tail and stale-physical-prefix mutations are
  retained in external `fix-evidence/worker-calibration-SCRATCH/audit/`.
  The target, whole-prefix boundary, TTL and backward-clock refusals are unchanged.)
- [x] Actual batch-loop controls show new launches/results/timings on hits; omitted
  current recording, cached results, stale key and premature publication go red.
  (2026-11-15: `CalibrationLoopTests` and `LaunchObservationTests` pass in both
  c0df CPU cohorts. Controlled 15/10 prefixes still perform current work and record
  current successful synchronization. Omitted-observation, skipped-work, stale-key
  and premature-publication mutants are red in the preserved worker audit logs.
  R12 final loop/prefix controls pass 15/15. No cached timing or result is imported.)
- [x] Upload, launch, download, cleanup, late assembly, loss, clear and dispose
  controls invalidate without replacing a primary; weak GC proves nonretention.
  (2026-11-15: `CalibrationLifecycleTests` and
  `CalibrationLoopTests.SuccessfulLoopIsNotPublishedBeforeResultAssemblyCommits`
  pass in both c0df CPU cohorts, covering all owner boundaries, the primary error
  and weak workload identities. The original failure/retention controls are retained.)
- [x] Real CUDA cold/hit/changed-input observations, unchanged 500 ms budget facts,
  original 5x/80%/115% throughput, science/bits and both platform matrices pass.
  (2026-11-15: actual c0df Windows and Ubuntu-24.04 budget phases pass 5/5 each,
  including `ActualCudaReportsColdHitAndChangedInputWithExactResults` (cold 1, hit 15,
  changed 1.  current coverage 31 each), both 16,384-case budgets and both P1 rows.
  Both full CUDA architecture/science/bit cohorts and separate original throughput
  facts pass. External audit `fix-evidence/final-candidate/` source/result/TRX
  receipts are `c0df7b146ffd-windows-budget-phase/`, `c0df7b146ffd-linux-budget-phase/`,
  `c0df7b146ffd-windows-cuda/`, `c0df7b146ffd-linux-cuda/`,
  `c0df7b146ffd-windows-throughput/` and `c0df7b146ffd-linux-throughput/`.)

Host evidence 2026-11-14: CalibrationIdentityTests, CalibrationPrefixTests,
CalibrationLoopTests and CalibrationLifecycleTests pass (37 host cases). Seven
runtime mutations fail behavior assertions: omitted scalar, stale input key,
fast tail, omitted current observation, skipped current computation, premature
publication and stale physical prefix. Logs: worktree SCRATCH/audit/mutant-*.log.
All acceptance boxes remain pending integrated review and actual CUDA gates.

⚠ 2026-11-15: the preceding "All acceptance boxes remain pending" sentence is
retained as the 2026-11-14 implementation handoff state. The six criteria now
carry source-pinned c0df host and actual Windows/Ubuntu-24.04 device evidence.
Their completed status supersedes that pending handoff; it does not turn the
old 37-case host run or its mutation logs into hardware evidence.
The measured prefix remains an empirical probe, and all original gates remain.

## Taboos

- No tolerance, approval, case count, median, timing-domain or numerical change.
- No disabling a bounded device budget, importing saved timings, strong workload
  references, unbounded cache, runtime-generated code or compiler-option change.
- No claim that prefix time proves merged time, or that identical outputs prove
  timing safety. Preserve cold-start controls and every actual overrun observation.
