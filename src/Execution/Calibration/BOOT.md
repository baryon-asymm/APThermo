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

- [ ] Cold and every incompatible workload start at one; a compatible live repeat
  reuses only the observed prefix, then retains current 2x and all static bounds.
- [ ] Machine-enumerated buffer mutations, seed A→output B→new input B, scalar,
  view type, table, launcher, plan and budget changes cause misses; clone A may hit.
- [ ] Prefixes never come from tail timing, rounding, partial completion or an
  over-target launch; expiry before match/launch and backward clock take cold path.
- [ ] Actual batch-loop controls show new launches/results/timings on hits; omitted
  current recording, cached results, stale key and premature publication go red.
- [ ] Upload, launch, download, cleanup, late assembly, loss, clear and dispose
  controls invalidate without replacing a primary; weak GC proves nonretention.
- [ ] Real CUDA cold/hit/changed-input observations, unchanged 500 ms budget facts,
  original 5x/80%/115% throughput, science/bits and both platform matrices pass.

Host evidence 2026-11-14: CalibrationIdentityTests, CalibrationPrefixTests,
CalibrationLoopTests and CalibrationLifecycleTests pass (37 host cases). Seven
runtime mutations fail behavior assertions: omitted scalar, stale input key,
fast tail, omitted current observation, skipped current computation, premature
publication and stale physical prefix. Logs: worktree SCRATCH/audit/mutant-*.log.
All acceptance boxes remain pending integrated review and actual CUDA gates.

## Taboos

- No tolerance, approval, case count, median, timing-domain or numerical change.
- No disabling a bounded device budget, importing saved timings, strong workload
  references, unbounded cache, runtime-generated code or compiler-option change.
- No claim that prefix time proves merged time, or that identical outputs prove
  timing safety. Preserve cold-start controls and every actual overrun observation.
