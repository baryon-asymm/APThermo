# Audit closure — 2026-11-14

Baseline: `1cfbaea8d1d75af670aebfb456e05702e72ec533`.
The owner requested closure of the combined Sol, independent second-pass and Claude
findings, plus justified improvements from the test-quality review. The external audit
retains the original reproductions; these stable IDs preserve the mapping.

## Current follow-up after the 36c28fb4 review — 2026-11-17

The independently verified review at `36c28fb46cf2a0e65a17f46c6169533ad5164a8d`
confirmed the bounded gaps below; it established no new numerical product defect.
The owner requested all four confirmed findings and the cosmetic XML cleanup.
The root aggregate is reopened until their source-bound checks pass. Earlier proof
indexes and the archived V5 decisions retain their original sources and scope.

The owner-held verified scope is
`closure-opus55-5f-20261117/review-opus55-36c28fb4/VERIFIED-REMAINING.md`.
New receipts live separately under
`closure-opus55-5f-20261117/fix-36c28fb4-abcd-e/`.

| Follow-up | Required correction | Current state |
|---|---|---|
| W1 / A / V5 / N17 | Classify content-producing Git history modes, embedded file selectors and output-widening modes; retain supported metadata and scoped reads. | Local pass: 71 self-tests, 180 real-CLI decisions and 30 independent review replays. Known history modes use the bounded grammar; unknown history options, `-L` and output-widening selections are refused. Shell redirection and Git-local directory bases remain distinct. |
| W2 / D | Report physical result-array length differences, including extra actual tails at equal logical dimensions. | Local pass: 43 comparison facts, including 29 new short/long/positive/message-budget controls. Old comparator fails 25 and retains four valid-shape positives; no production shape defect is claimed. |
| W3 / C / V6 | Qualify the remaining owner invariant, historical criterion and comparator summary as numerical/status/iteration equality with a separate metadata policy. | Reconciled owner/test wording, with dated corrections and original historical evidence retained. |
| W4 / B | Describe the fixed-width/transport parser corrections in the release notes. | Included in 0.3.1; complete blank fields retain zero semantics. |
| W5 / E | Combine four duplicate Transport XML summaries under their own types. | Local pass: all four compiled XML members have exactly one summary, retaining their descriptions. |

Local verification, 2026-11-17: a fresh 0.3.1 Debug build has zero warnings/errors;
39 structural protocol facts and 43 comparison facts pass without skips. Strict
protocol lint is 0/0, and delivery guards pass 28/28. The owner-held
`fix-36c28fb4-abcd-e/integration-dotnet-receipt-v1.json`, coordinator acceptance v2
and `root/root-hook-replay-v2.json` bind the actual checked file bytes and results.
Hosted verification and release rehearsal are still pending; these local results
do not relabel earlier GPU or hosted runs as checks of the new source.

F does not establish a missing date: the actual proof date is present in each
criterion's paragraph, and the protocol does not require a single physical line.
The zero-filled fixture and scalar-count observations do not establish additional
global coverage defects when the existing result/inventory controls are considered.

Publication preparation (N18): the owner requested a complete release on 2026-11-17.
Both NuGet package feeds already contain 0.3.0; the new version is 0.3.1. The packed
consumer surface is unchanged from the published baseline; snapshot additions concern
test infrastructure. `Directory.Build.props` and the new dated CHANGELOG section name
0.3.1. The tag remains subject to the exact-commit release rehearsal through Pack;
publication uses the existing protected Trusted Publishing job.

## Historical follow-up after the 5f2b53be review — 2026-11-17

⚠ 2026-11-17: the previous acceptance for 779eeace below did not establish complete
closure. Independent verification at `5f2b53be569fd63d15f0f30d6666d5b1fea503ec`
reproduced accepted truncated thermo headers and species properties. N3's whole-record
claim and N17's ordinary-command coverage were broader than their actual guards.
At the start of this follow-up the root aggregate was reopened; earlier measurements
remain evidence for their named sources, without being relabelled as proof of these
follow-up corrections.

The owner-held archive contains the verified scope at
`fix-evidence-20261117/review-opus55-5f/VERIFIED-REMAINING.md`. Follow-up receipts live
under `fix-evidence-20261117/closure-opus55-5f-20261117/` in the same archive namespace.

| Follow-up | Required correction | Current state |
|---|---|---|
| V1 / F1 / N3 | Reject physically incomplete required thermo header/property fields; retain complete blank-to-zero semantics and located errors. | Passed: Data 88/88; old source fails twelve cases, individual guard removals fail eight/four. Project and freshly packaged CLI reject both archived malformed inputs at their own source lines. |
| V2 / F4 | Protect separately nonfinite Mach and vacuum impulse at the actual station writer, without duplicating algebraically identical figures. | Passed: two isolated synthetic station controls; each compiling guard-removal mutant fails its corresponding assertion. No physical public-input failure is claimed. |
| V3 / F10 / N13 | Cover unsupported public fields and later elements of the declared result arrays. Existing later-index controls remain in force. | Passed: six comparison controls, unsupported-field and first-Cp-only mutants rejected; every generated array element is independently changed and restored. |
| V4 / F2–F5 | Reconcile the five open leaf criteria, actual clean/incremental builds, changed guards and source-bound evidence before closing this aggregate. | Accepted: all five leaf records identify actual evidence. Rebuild/incremental builds and generated populations pass; exact local and same-platform hosted comparisons retain all original identities and add 22 source-derived cases. |
| V5 / F6 / N17 | Check ordinary unscoped Git content/history commands and find roots/nested reads; retain safe scoped and metadata operations. | Passed: 59 self-tests; nine pre-fix failing subcases. The real-hook replay now refuses all six archived gaps while retaining all eight earlier control decisions. |
| V6 / F8 | Correct the numerical/metadata comparison wording, production chunk traversal and repeated XML summaries. | Reconciled: Execution test/API messages name numerical outputs, statuses and iterations, chunk documentation names production traversal, and XML summaries describe their actual members. |

Follow-up acceptance, 2026-11-17: the implementation is
`d7cc333ab8d22a9223bc8078ef1c72cc279ad9c6`. The owner-held
`closure-opus55-5f-20261117/root/runtime-proof-d7-v1.json` binds the source, raw
reports, controls and independent readbacks (SHA-256
`7ea6a5de8a7628a984a630f38135f9e73cd7bf93d556f57fe02654309513d31a`).
The local fast and end-to-end selectors passed 7,818 and 121 cases in 256.33 and
261.66 seconds, within the unchanged 300/600-second limits. The one pre-existing
optional CORE-MATH corpus case remains explicitly skipped, as on the baseline.

[CI 44114557750](https://github.com/baryon-asymm/APThermo/actions/runs/44114557750)
passed all six hosted lanes: 7,939 cases per operating system, without skips. The
independent artifact parser checked all eleven discovered projects per OS and all
raw outcomes. [Trace 44113823079](https://github.com/baryon-asymm/APThermo/actions/runs/44113823079)
passed both full CPU scan facts on each OS. These are new d7 results, not relabelled
779 results.

Fresh CI packages identify d7. All fifteen project PE/PDB pairs, tracked Git source
checksums and embedded generated source pass the reused read-only inspector. The
library/tool Data assembly and symbols agree, and the extracted packaged CLI refuses
both malformed databases. This is a new offline package check; HTTP SourceLink
availability was not re-fetched. The CI consumer and installed-tool steps also pass.

No new GPU timing campaign was needed: the generated runtime-scope record compares
immutable Git inputs and identifies only the host Data guards, software controls,
Python scope guard and XML/log changes. Numerical/kernel code, committed data,
fixtures, approvals and workflow gates remain unchanged. Actual hardware and timing
receipts retain 779 as their source, without asserting binary identity to new packages.

The Fable N3 recommendation to reject blank coefficients is refined: physically
absent columns fail, while complete blank fields retain the existing zero semantics.
Its N1 cold/warm ratio is not a controlled historical cold-to-cold regression. F3's
late defensive solver refusal and F11's large finite vacuum impulse do not justify
an artificial injection seam or a new model floor. The detailed distinctions remain
in the verified review and `closure-opus55-5f-20261117/root/FABLE-RECONCILIATION.md`.

The verified observations do not justify a new near-unity model floor, an artificial
solver injection seam, changed numerical approvals or relaxed performance limits.
Interactive console side-effect wording and manual GPU-evidence prerequisite messages
are usability clarifications, not newly demonstrated runtime failures.

## Historical re-audit acceptance for 779eeace — 2026-11-17

The tested implementation is `779eeacee40cadabec3a2647ddf4b043309c37b4`. The owner-held
`fix-evidence-20261117/root/final-proof-index-779-v1.json` binds the runtime receipts to
this commit (SHA-256
`c8fa7ccf5a2305a372b9cd0de030a0fa151dfd4eb916a7961d4b9b288218cac5`). The archive retains
the original reproductions, failed runs and controls under their own source identities.

The fast suite passed 7,796/7,796 in 247.28 s. The end-to-end suite passed 121 cases in
286.38 s, with the same one optional CORE-MATH case skipped as on ec01. The
[CI](https://github.com/baryon-asymm/APThermo/actions/runs/44048478640) and
[Release](https://github.com/baryon-asymm/APThermo/actions/runs/44048539081) hosted
matrices each passed 15,834 cases without skips.
[Trace](https://github.com/baryon-asymm/APThermo/actions/runs/44048478774) passed both
expected cases on each OS. Each GPU job passed 452 selected cases on the RTX 5070 Ti.
The initial Linux launcher exited before receiving a job; the recovered listener ran the
original job once. Both listeners exited and the temporary Linux network closed.

Pack produced two package/symbol pairs. The inspector verified 15 packaged project
PE/PDB pairs, 262 tracked source URLs and 49 generated documents embedded in PDBs. On
each OS all twelve samples and the installed CLI matched approved outputs when run from
the packaged feed. The dispatch skipped both publishing jobs.

| Finding | Correction and retained evidence |
|---|---|
| N1 | Cold and median throughput have separate records. Release 44048539081 measured both against their existing platform references. The original one-observation cold references and the former medians do not establish cold-to-cold recovery. |
| N2 | The near-unity CLI witness failed on the review baseline. The corrected return guard passed the public regression and CPU and actual-CUDA binary64 classification on 779. |
| N3 | The readers reject physically truncated required columns. A full-width blank numeric field retains the original Fortran zero interpretation. The malformed-row controls and the complete Data suite exercise the distinction. |
| N4 | The actual CP866 console experiment failed at 1a9 and passed at 1368 with the original code page preserved and redirected UTF-8 output without a BOM. The 779 CLI and owned-process suites passed separately. |
| N5 | The test calls the area-iteration residual calculation and detects a compiled guard bypass. Its input is a synthetic residual witness. |
| N6 | HP/SP reject nonfinite targets before writing output. The frozen estimate retains the zero sentinel. The focused controls and full CPU suites passed. |
| N7 | The attempt records the domain-edge cause independently of its return status. A synthetic bank exercises the actual selection path and its compiled negative control. |
| N8 | The reader rejects invalid terminators, trailing records and duplicates according to the documented policy. Source-frozen negative inputs and the Data suite cover the cases. |
| N9 | The individual acceptance records identify the actual source of their prior and current results. Owner-process, GPU inventory, calibration and recovery cases now have follow-up receipts for 779. |
| N10 | Superseded text remains in node-local history with dated pointers. The full move guard for 779 to 5127b233 preserved all active criteria and reported no missing lines or unresolved anchors. |
| N11 | The guard checks the real hosted project inventory, failure propagation, analyzer configuration and implicit xUnit entry points. Each 779 hosted workflow produced six manifests covering the eleven expected projects per OS. Both platforms ran the two expected Trace facts. |
| N12 | Only process disappearance at the actual stat read is handled. Other read and enumeration errors propagate. Both Linux workflows passed all ten process-observation cases and five pre-start cases. |
| N13 | The generated result inventory and mutation controls exercise array and structure fields. Per-platform GPU receipts identify 88 hardware cases with completion markers, four driver-only cases and the remaining selected bit checks. |
| N14 | Schema, reader and CLI controls cover invalid inputs and transport exit requirements. The public example and schema were validated by the Docs suite on 779. |
| N15 | The seeded/frozen estimate, the CEA Cp/Cv convention and the independent general-exponent Cp/H/S oracle have separate controls. The retained exponent mutation fails the analytical check. |
| N16/Z3 | The public API, reflection dependencies and documentation describe the implemented boundary and the diagnostics channels the guard checks. The Protocol suite and strict tree lint passed. |
| N17 | The coder-scope guard covers ordinary implicit directories, searches and Git path arguments. The permitted stdin case and rejected out-of-scope cases are in the frozen controls. The hook remains a heuristic. |

N18 concerns choosing a version before a future publication. It is outside this
correction.

## Reopening before follow-up verification — 2026-11-17

The independent review of `1a9ac02f1e33e915aff3877e47898d413f39de26` found further
confirmed product, verification and documentary defects. The root aggregate is
reopened while they are corrected and checked on the integrated source. The verified
scope and distinctions are in the owner-held `VERIFIED-REMAINING-1a9-20261117.txt`;
the original report is `REAUDIT-1a9-FABLE52.md` in the archive named below. In
particular, the earlier cold guard did not establish recovery of fresh-workload
performance. Previous CPU, GPU and package receipts retain their original source.

## Historical re-audit acceptance — 2026-11-16

The implementation source is `c0ebe7047a0756dbb054e6853fa787a3cc185a11`.
The earlier 76 A01–A41/R01–R19/T01–T16 identifiers overlap and are not 76 distinct
defects. Their original register, prior fixes and historical proofs remain in the
owner-held archive already named below. The current 76-row mapping and Z1–Z14 are
`fix-evidence/closure-matrix-c0.md` and `closure-matrix-c0.json` in that namespace;
their SHA-256 values are `ed5073c184ae75395372d8c8b23879693f9f00e92aaa0f1dddd46c00191af340`
and `82e913c6457433181a25cccc614340a565a2239c72227944283abcee323c6b6c`, respectively.

[CI](https://github.com/baryon-asymm/APThermo/actions/runs/43861325471) and
[Release](https://github.com/baryon-asymm/APThermo/actions/runs/43861338174)
hosted lanes each passed 15,588/15,588, without skips. [Trace](https://github.com/baryon-asymm/APThermo/actions/runs/43861325374) passed its two facts on each OS.
Both GPU platforms passed 451/451 (449 remainder plus both original sweep facts),
including 87 hardware and 4 executed driver-only CUDA markers per OS, without skips.
The unchanged warm gates passed at ratios 30.46/25.98 and per-step medians
1.342e-7/1.272e-7 s on Windows/Linux; the separate cold guard passed too
(`root/cloud-c0/gpu/windows-independent-receipt.json` and
`root/cloud-c0/gpu/linux-independent-receipt.json` in the owner-held namespace).
Pack succeeded and publication jobs were skipped. Actual Windows/Linux consumers matched the downloaded package bytes,
and PDB/SourceLink evidence passed (`root/cloud-c0/consumer/independent-consumer-receipt.json`
and `root/cloud-c0/package-inspection/combined-package-source-receipt.json` in the
owner-held namespace). Run pages identify the runs; uploaded
artifacts may expire. Earlier f21 acceptance and failed 589 hosted results retain their
actual source and outcomes.

| Delta | Current correction and proof location |
|---|---|
| Z1 | Original throughput approvals and arithmetic remain; separate first-cold record and cold/hit/hit checks have host red/green controls. Frozen historical reference is one reported sample per OS, not a stability estimate. First cold values 2.681e-7 Windows and 2.686e-7 Linux s/step passed their predeclared bounds. |
| Z2 | Recovery candidate restores all condensed slots with winning state; frozen identical CPU probe is red before/green after. Actual c0 CUDA witness and bit gates pass on both platforms. |
| Z3 | This ledger names tests, run pages and the owner-held archive namespace; external evidence is explicitly located, not treated as a broken in-clone relative link. |
| Z4 | Condensed eligibility/open-below-policy is in the actual public consumer contract. |
| Z5 | CPU cache bypass is tested after compilation against real seeded session state. |
| Z6 | Actual Verify exhaustion dispatch has controlled positive/mutant tests; the synthetic seam is not claimed as an eight-boundary physical witness. |
| Z7 | Real Thermo buffer construction fault exercises rollback, with a host-only test seam and kernel-reachability check. |
| Z8 | State schema enforces target cardinality and flow parity; independent validator matrix covers 288 combinations and two array controls. |
| Z9 | ThermoFile tail semantics have contract and negative controls. |
| Z10 | Delivery guards verify exact discovered GPU identities against both TRX phases, and CI/Release/Trace command policy is aligned. Actual GPU and Pack evidence is separate. |
| Z11 | A shared bounded process owner handles ordinary descendants, output and primary/secondary failures; pre-exec identity is retained and verified before signal/START. Frozen Linux exec-transition, parent-liveness and wrong-birth mutations are red with restored controls green; Windows parent mutations are also red/green. The 589 hosted failure stays historical. |
| Z12 | Refusal and real-CUDA markers are distinct; hardware verification is not inferred from CPU-forbidden runs. |
| Z13 | History pointers, chronology, document limits, host/kernel scope and loop-live rule are reconciled; BeO historical approval is not rewritten. |
| Z14 | Invalid schema type and nonfinite Python comparison inputs/tolerances are rejected. |
| A36 | Public failed-state inventory covers all supported state and figure fields with meaningful controls. |

Current source-bound criteria are in [Equilibrium](src/Equilibrium/ACCEPTANCE.md),
[Recovery](src/Equilibrium/Recovery/BOOT.md), [Performance](src/Performance/ACCEPTANCE.md),
[Thermo](src/Thermo/ACCEPTANCE.md), [Execution tests](tests/Execution.Tests/ACCEPTANCE.md),
[process ownership](tests/Harness/ProcessOwner/BOOT.md) and [root acceptance](ACCEPTANCE.md).
Earlier tables, corrections, numeric approvals and command/gate semantics remain below;
no f21, 589 or intermediate native run is relabelled as c0 evidence.

## Historical re-audit reopening at 08bf — 2026-11-16

The independent re-audit of `08bf26c3` confirmed further gaps. At that stage the
aggregate was reopened pending correction and re-verification. Earlier headings and
claims below describe their dated source and stage, including the f21ac451
acceptance; they do not assert acceptance of later changes. The detailed re-audit
and source-bound receipts are retained in the owner-held external archive rooted at
`C:/Users/edwar/.codex/visualizations/2026/11/13/01a1d4c2-d403-7472-a5aa-1139466cb9c9/claude-reaudit-20261116/`:
`REAUDIT-REPORT-08bf26c3.md` and `fix-evidence/`. The prior audit archive is its
sibling `full-audit-20261113/fix-evidence/`; the f21a index cited below is inside it.
These paths are not in the clone, and a historical `SCRATCH` name alone is not a
stable location: use the owner-held archive index or the named test and source.
Historical f21a run pages are [CI](https://github.com/baryon-asymm/APThermo/actions/runs/43742270400),
[Trace](https://github.com/baryon-asymm/APThermo/actions/runs/43742270272), and
[Release](https://github.com/baryon-asymm/APThermo/actions/runs/43742272942).
These identify their original runs; available GitHub artifacts may expire independently.

## Accepted implementation and runtime verification — 2026-11-15

The A01–A41, R01–R19 and T01–T16 obligations have reviewed fixes and current
evidence. The generated map retains 76 overlapping stable IDs, not 76 distinct
defects. Earlier criteria, measurements and failed/cancelled histories remain below.

Actual source `f21ac451c286bcbea88703b7bf4b6e90af35709e` passes CI `43742270400`,
Trace `43742270272` and Release `43742272942`. CI and Release each pass 7,672
cases per OS, without skips, over the generated 11-project partition. Each GPU
passes all original 450 identities exactly once: 448 remainder cases and both
shared-sweep facts in a fresh process, with 12 TRX per OS. Original work, five
warm-ups, three samples including the cold sample, 500 ms launch/freshness
policies and every throughput/bit/numerical gate remain unchanged. Windows records
ratio 20.32 and per-step 1.271e-7; Linux 26.10 and 1.328e-7.

Current Debug passes 7,614 fast cases in 191.780 s against 300 s, and 59 EndToEnd
cases in 194.378 s against 600 s. No skips; the full math case passes with all
three pinned corpus hashes. GPU runners start after local checks and terminate
after their jobs. Pack produces four archives. PDB/SourceLink verification derives
7 assemblies/7 PDBs/217 documents for the library and 8/8/288 for the tool, all at
the exact source. Dispatch publishing and release creation remain skipped.

Runtime proof: external `fix-evidence/github-f21a/final-proof-index-f21a-v1.json`,
SHA-256 `7aca04b5eb9363f4e6a3fe65a66e61a0c6b7babe5a355f7c58b123dcda343312`.
The later documentation commit receives separate unchanged-code identity and
actual CI/Trace verification, without claiming its own future hash or result.

The R10 remedy isolates the original shared pair. Runtime C# retains the reviewed
observation implementation; numerical sources, data, fixtures, samples and scientific
approvals retain their generated identities. Observation has finite cost.
Source `1a8aca20` remains a failed Windows 449/450 run: its third matching published entry
expired at 1.0437997 s. Its failed actual record is fresh within that fact. The
separately declared same-source fresh pair passes 2/2, ratio 21.31, per-step
1.261e-7, repeat ages 4.9066/5.6060 ms. This supports process isolation; the pause
cause remains unknown. The earlier a930 Linux diagnostic remains separately scoped.
Accepted `0f7`, the failed `84` tree check, `49` CLI, `6878` Windows GPU,
`5d09` setup, `463` Linux GPU, and owner-superseded `76` cancellation retain their
real outcomes. Current acceptance
supersedes readiness without relabelling those stages.

## Latest GPU verification and independent timeout review — 2026-11-15

2026-11-15: source `a93074210a6c02646c2197b764334a83e221cefd` adds bounded
calibration observations without a performance-policy change. The one predeclared
original Linux throughput fact passes: ratio 24.73, per-step 1.326e-7, cold/hit/hit.
The matched entries are 6.3453/7.3300 ms old after upload, within the original
500 ms lifetime. Windows/Linux host controls pass the same 148 identities; five
diagnostic mutants and the seeded-session invalidation mutant fail as intended.
An independent review caught and closed that seeded-session test precondition gap.
External root diagnostic summary SHA-256:
`97b7cc2615e987428aaf2e433d44c53531bc3c41e7609ede59fb81dcfb9f43ed`.
This isolated success does not establish the historical miss cause or close R10's
full Release acceptance. Full current CI/Trace/Release verification is next.

⚠ 2026-11-15: actual source `463cf30f14ebff12221d2d04f2ea0fca2c8fba0a`
passes CI/Trace, every hosted cohort (7,649/7,649 per OS), corrected Debug budgets
and Windows GPU (450/450). Linux GPU job `132858167211` fails only throughput:
449/450, ratio 14.01 versus 80% of approved 25.92 (20.736). Its per-step median
2.709e-7 also exceeds 1.42025e-7 arithmetically; that second assertion was not
reached. Current physical maxima stay below 45 ms, with cold/cold/hit decisions.
Pack skips. R10 remains open; serialization alone was insufficient for Linux
acceptance. Add bounded actual decision/age/publication observations before any
policy change. Do not infer the unsampled miss cause or relabel this failed run.

⚠ 2026-11-15: subsequent source `5d0906d7913fd3cad12cfd2f3887452789324302`
passes local merged Release checks and corrected Debug sets, but its actual CI
`43721105782` and Release `43721163754` fail Windows Protocol probe preparation.
CI loses the parent PID during Capture in pipe mode both; Release cancels the
two-second ready-file read in mode stdout. Each has 162/163 Protocol passes;
Ubuntu has 7,641/7,641. The helper-completion assertion was not reached. R17's
current hosted acceptance remains open pending deterministic ready/clock staging.
Trace passes; GPU/Pack skip. Preserve the exact failed logs/TRX; the unsampled
historical timer/scheduling cause is not inferred from a later successful run.

Source `68781f6972b4f2f7bf0ffdd63985c0cbba4ade35` passes CI `43710324828`,
Trace `43710324831` and all six Release hosted lanes (7,625/7,625 per OS, no skips).
Release `43710385057` fails Windows GPU job `132803574118`: 449/450 pass, with
only the 115% per-step throughput guard failing at 217.1% of its approved value.
R10's current integrated verification is reopened; the original native/0f7
successes remain successful for their actual sources. Linux GPU and Pack skip.
The generated three-sample record is cold/hit/cold, with 20/7/20 launches; its
largest individual launch is below 43 ms. This is a performance refusal, not a
process timeout or a 500 ms launch-budget violation. Its historical cause is unknown.

Independent GPT-6 Astra Ultra review of committed source and archived logs adds:

| ID | Confirmed gap / required correction | Current state |
|---|---|---|
| R16 | Self-hosted solution projects can overlap the shared CPU/GPU measurement; require sequential project execution and fresh per-project evidence while retaining every selected case/sample/gate | Design; actual overlap is known, its causal role in the 217.1% result is not established |
| R17 | PythonProcess bounds parent exit but can await inherited stdout/stderr forever | Design; deadline must span process and streams, with bounded owned cleanup and old-red controls |
| R18 | Documented partial tree-kill AggregateException can escape cleanup and replace timeout/cancellation plus partial output | Design; preserve primary and secondary diagnostics with injected old-red controls |
| R19 | Cold-call diagnostics duplicate the runner's policy values and can silently drift | Design; consume one immutable actual policy, prove alternate-policy drift without real CLI work |

The review keeps hang guards, owner suite budgets, launch targets and performance
thresholds in their distinct roles. It does not justify a timeout/threshold increase,
dropping a cold sample or treating historical overlap as proof of causality.
External source-bound receipts: `fix-evidence/github-6878/`; independent report:
`astra-timeout-review-v1.md`. The expanded register has 76 overlapping obligation
IDs (41 original A, 19 verification R, 16 test-quality T), not 76 distinct defects.
The prior 71/72-ID accepted/pending registers below retain their historical scope.

## Additional delivery finding R15 — 2026-11-15

R14's actual process controls pass192/192 Fixtures in all four source49 hosted
cohorts. Source49 CI and Trace pass, but Windows Release job132779316611 fails one
Docs cold CLI example at its unchanged five-minute deadline, with zero stdout and
stderr; 7,615/7,616 tests pass. The failed invocation and resource state were not
logged. This is a separate open finding R15: preserve process-example context and
isolate hosted project workloads while retaining every command and deadline.
The raw log proves overlapping Cli.Tests/Docs.Tests, not the timeout's cause.
Current readiness remains open until new actual evidence accepts R15 as well as
the reopened aggregate. The failure occurred on GitHub's hosted Windows VM; it does not establish a
performance problem on the local reference host. Prior successful/failed/cancelled
runs below keep their actual sources.

## Reopened verification — 2026-11-15

Final-head CI `43695098528` at `84e0cb09`, Ubuntu job `132753766806`, fails
`DeadlineRefusesProcessOrInheritedPipeHang` in tree mode again: 187/188 Fixtures
facts pass; the child-live assertion fails after about two seconds. The log has
no PID/kernel-state snapshot, so its exact failed instant is unknown. R14/T14 and
the root aggregate are reopened while the observation/cleanup boundary is checked.
The complete earlier acceptance below is the prior candidate state, not current
readiness. The frozen successful c0df/0f7 runs remain successful for their sources.

The .NET contract describes Kill as asynchronous; parent exit plus pipe EOF does
not establish that every descendant has reached its terminal kernel state.
The corrective work must separately observe the owned descendants within the
remaining original cleanup budget and still reject a truly live survivor. No
new post-timeout grace period, relaxed timeout, dead-state classification or
retry-until-green is accepted. A controlled known-answer witness must fail the
previous immediate observation and pass the corrected bounded observation.

## Current audit closure — 2026-11-15

All recorded obligations A01–A41, R01–R14 and T01–T16 are accepted within their
documented scope. The generated `fix-evidence/closure-obligation-map.json` records
71 overlapping stable IDs (41 original findings, 14 verification findings and
16 test-quality observations), with no unresolved source references. This is an
obligation index, not a defect count or a claim about unexamined behavior.

| Obligations | Current state | Dated evidence |
|---|---|---|
| A01–A41 | Closed 2026-11-15 | Generated 71-ID map, native evidence and final runner-path proof index |
| R01–R14 | Closed 2026-11-15 | Same index; R13 actual hosted native import/binding and R14 actual current process-control suites included |
| T01–T16 | Addressed 2026-11-15 | Same generated map links each observation to its concrete repair and verification |

Final native execution retains its actual source
`c0df7b146ffdd3addb733e8931aaabf0ca5fc660`. The explicit unchanged-runtime comparison
to `0f7ecb6f4fff173fc8716e93655303f09b412d7c` is retained at
`fix-evidence/root/remote-integration-0f7e-v1/runtime-carryforward.json`.
Current GitHub evidence is CI `43691428901`, hosted trace dispatch `43691604795`
and Release dispatch `43691442012`, all at the exact latter source. Release passes
through both self-hosted preflight/CUDA jobs and Pack; Publish to NuGet and Create
GitHub Release are skipped. Actual source/run/job/artifact hashes and the applicable
node criteria are recorded in `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.

⚠ 2026-11-15: the slice tables and earlier handoff paragraphs below retain their
original dated states, including “full platform/hardware matrix pending”, “whole
CI pending” and “hosted confirmation pending”. Those statements described their
earlier source and verification stage; they are historical evidence, not current
open obligations. The dated current state above supersedes them without deleting
their wording, failures, local-only limits or measurement provenance. In particular,
the f769 CI/Release failures remain failures and the d454 positive runs retain d454
as their actual source; neither is relabelled as a newer successful execution.

## Acceptance

A row closes only after its old-failing/positive controls and affected checks pass.
Document-only corrections use an addressable contract comparison instead of synthetic
tests. The final integrated CPU, CUDA, platform and package checks remain separate.
Historical measurements and active criteria are preserved; no blind snapshot approval,
tolerance weakening or diagnostic suppression is an acceptable closure.

⚠ 2026-11-15: the original A01-A41 rows retain their dated repair-handoff evidence
verbatim. Earlier "pending" phrases there describe that handoff; current native
acceptance is recorded in the final native table below. GitHub-specific evidence
for A23/A33/A39 and the workflow criterion remains open, as does the root aggregate.

| ID | Obligation | Status | Evidence |
|---|---|---|---|
| A01 | Eligible polymorph stability | Local pass 2026-11-14 | `25b95f55`; independent integration regression 18/18, full platform verification pending |
| A02 | Immutable database and problem snapshots | Local pass 2026-11-14 | `e5e3bfe5`, `782ced8f`; Data 54/54, public integration controls 28/28; final matrix pending |
| A03 | Cleanup preserves the primary failure | Local pass 2026-11-14 | `6e9fcfb1`; root multiple-fault/lost-session integration 7/7; remaining ownership chains tracked under A04 |
| A04 | Rollback partial table allocation | Local pass 2026-11-14 | `233c8cca`, `395ef758`, `05ff0258`, `0a37d7aa`, `0e8b032e`; table/wrapper transactions, real CPU copy-fault controls and primary-preserving diagnostics; root Transport 20/20 |
| A05 | Exact condensed-set identity | Local pass 2026-11-14 | `14d875bb`; Performance integration controls 32/32; architecture follow-up in progress |
| A06 | Ordinary-throat reference sensitivity | Local pass 2026-11-14 | `14d875bb`; finite corrupted ordinary-throat controls included in 32/32 |
| A07 | Finite transport comparison | Local pass 2026-11-14 | `14d875bb`; Transport integration controls 73/73 |
| A08 | Benchmark comparison rejects nonfinite mismatches | Local pass 2026-11-14 | `ed8acaae`; whole-entry controls included in root 90/90 contract/comparison run |
| A09 | Actual boundary-walk cap verification | Local pass 2026-11-14 | `14d875bb`; actual automaton cap and mutation controls; root Performance 32/32 |
| A10 | First-maximum ordering oracle | Local pass 2026-11-14 | `14d875bb`; late-higher-peak rejection; root Performance 32/32 |
| A11 | Generated performance field inventory | Local pass 2026-11-14 | `14d875bb`; reflected six-field corruption controls; root Performance 32/32 |
| A12 | Pinned full math corpus integrity | Local pass 2026-11-14 | `7d42fae6`; seven portable refusal controls; root complete 2,266,122-input rounding campaigns passed on Windows and Ubuntu-24.04, with pinned identity and no skip |
| A13 | Internal frozen-state finite guards | Local pass 2026-11-14 | `4755241d`, `77afac60`; root finite-state/bit controls 17/17, representable extreme positive cases retained |
| A14 | Internal transport finite temperature | Local pass 2026-11-14 | `14d875bb`; root Transport controls 73/73 |
| A15 | Windows and Linux CUDA-driver detection | Local pass 2026-11-14 | `17f048d2`; shared Windows/Linux name predicate and ordinary/forbidden-module controls; real Ubuntu-24.04 CPU process/planning controls 36/36, final full matrix pending |
| A16 | Approved snapshot write protection | Local pass 2026-11-14 | `200a1bc2`; root Fixtures 68/68, including ordinary/link output controls |
| A17 | Schema validator semantics and keyword coverage | Local pass 2026-11-14 | `200a1bc2`, `38488d5a`, `e486c513`; exact decimal semantics and strict URI pointers; root Fixtures 187/187 and original independent probes reject all reproduced bypasses |
| A18 | Chunk enumerator integer boundary | Slice pass 2026-11-14 | `17f048d2`; finite int.MaxValue covering and ordinary/empty chunks in boundary controls; root Protocol 131/131 |
| A19 | Pinned and atomically published merge candidate | Local pass 2026-11-14 | `a6ff702c`; merge guard self-tests 72/72, independent rerun |
| A20 | Preservation of active acceptance criteria | Local pass 2026-11-14 | `a6ff702c`; history guard 35/35 and baseline-to-HEAD criteria check |
| A21 | Complete documented signatures | Local pass 2026-11-14 | `3b9f5f7e`, `739a69b4`, `674a63c7`; explicit base/interface promises checked; root integrated Protocol 154/154 |
| A22 | Complete source and build-manifest node discovery | Local pass 2026-11-14 | `7ec349d6`; root lint self-tests 84/84 and strict lint 0/0 |
| A23 | PowerShell preflight failure propagation | Local pass 2026-11-14 | `4e5d1494`, `a3191c24`; root guards 20/20, real Windows PS5.1 and Ubuntu-24.04 preflight pass; GitHub runner proof pending |
| A24 | Current protocol-linter negative controls | Local pass 2026-11-14 | `a6ff702c`; lint self-tests 75/75, strict lint 0/0 |
| A25 | Inclusive sweep limit | Local pass 2026-11-14 | `e8b9bf46`, `695d9715`; decimal maximum regression corrected after independent review; root CLI CPU 177/177 (`fix-evidence/root/cli-integration-current.trx`) |
| A26 | Input/output schema parity | Local pass 2026-11-14 | `6bf771aa`, `8406c714`; conditional shape controls 42/42, real zero-mass InvalidInput output admitted; root CLI 177/177; reviewed input-schema bytes approved and four documentation consumers pass |
| A27 | Finite fixture tolerances | Local pass 2026-11-14 | `ed8acaae`; finite-intermediate-overflow control included in root Fixtures 68/68 |
| A28 | Current contract and guide claims | Local pass 2026-11-14 | Owning numerical/public claims corrected, `9dd497c9` benchmark launch/fallback/setup scope, `8b5cbfa7` exact sample snippet, `9ea4c213` cleanup scope; final docs consumer run pending |
| A29 | Unambiguous chemical-system cache key | Local pass 2026-11-14 | `782ced8f`; framed-name collision and positive controls in root public 28/28 |
| A30 | Release propellant references at Dispose | Local pass 2026-11-14 | `782ced8f`; reachability and cleanup-failure controls in root public 28/28 |
| A31 | Scale-invariant group normalization | Local pass 2026-11-14 | `782ced8f`; subnormal group control in root public 28/28; ordinary records unchanged |
| A32 | Complete batch-composition comparison | Local pass 2026-11-14 | `31c28741`; union/condensed/zero-key controls in root public 28/28 |
| A33 | Literal release tag shell arguments | Local pass 2026-11-14 | `4e5d1494`; adversarial valid-ref and fake-gh controls in root 20/20; affected GitHub runner proof pending |
| A34 | Literal changelog version matching | Local pass 2026-11-14 | `4e5d1494`; exact/prerelease/build-metadata, lookalike and empty-section controls in root 20/20 |
| A35 | Multi-floor HP/SP objective selection | Slice pass 2026-11-14 | `b8f90090`, `77d9c7b2`; cold bank/floor controls 34/34, warm controls 16/16, public and CPU witness consumers; final integrated platform/hardware matrix pending |
| A36 | Failed-state public postcondition | Local pass 2026-11-14 | `14d875bb`, `782ced8f`; internal/public failure controls, NoGasPhase exception and prior-success preservation |
| A37 | Public condensed-model applicability | Local pass 2026-11-15 | `668939f6`, `14d875bb`, `662845e2`, `d734d3cd`, `fbe9d43f`, `f666c6fa`; public contracts plus a separate boundary-fixture sidecar record exact inputs, fit/admission bounds and source hashes. Root metadata/public controls 17/17; five provenance hashes independently matched |
| A38 | Disposal overload inventory | Local pass 2026-11-14 | `31c28741`; exact MethodInfo inventory and same-name wrong-overload negative control |
| A39 | TraceGas scan automation | Local pass 2026-11-14 | `56345fe4`; root runner guards 8/8 and actual Windows discovery/execution 2/2 with exact TRX/matrix; hosted runner proof pending |
| A40 | Tail-test name and established behavior | Local pass 2026-11-14 | `14d875bb`; policy-only name states the constants it verifies |
| A41 | Required database section transition | Local pass 2026-11-14 | `e5e3bfe5`; missing/duplicate/reordered section controls in root Data 54/54 |

## Findings from verification of the fixes

These entries preserve additional reproduced mechanisms without renumbering the
original combined audit. Every open entry is part of the same closure obligation.
The independent reports and reproducible controls are retained in the external
audit's `fix-evidence/independent-public-cli`, `independent-tooling` and
`independent-ownership` directories.

| ID | Mechanism and original area | Status | Evidence |
|---|---|---|---|
| R01 | Decimal quotient rounding at the inclusive sweep maximum (A25) | Slice pass 2026-11-14 | `695d9715`; old source 1 red/7 green, corrected boundaries 21/21 and full CLI 177/177 |
| R02 | Nonfinite conversion bypass in JSON field comparison | Local pass 2026-11-14 | `7bbb636b`; old 4 red/7 positive, corrected 11/11; root Fixtures 187/187 and independent original probe now rejects 1e400 |
| R03 | Binary64 rounding in schema numeric decisions (A17) | Local pass 2026-11-14 | `38488d5a`; exact normalized digits/exponents for integer/bounds/minItems/enum/const; 61 controls and root Fixtures 187/187 |
| R04 | Explicitly false documented base/interface contracts (A21) | Local pass 2026-11-14 | `674a63c7`; 23 base/interface controls, root Protocol 154/154; original independent wrong-base probes now reject |
| R05 | URI-fragment and malformed JSON Pointer resolution (A17) | Local pass 2026-11-14 | `e486c513`; old 32 red/4 positive, corrected 36/36 and root Fixtures 187/187; original encoded-ref probe selects the right schema |
| R06 | Copy failure before allocation result enters ownership (A04) | Local pass 2026-11-14 | `05ff0258`, `0a37d7aa`; real ILGPU CPU-buffer fault controls observe allocation layers and cleanup; root Transport 20/20 |
| R07 | Transport diagnostic storage can replace the primary (A04) | Local pass 2026-11-14 | `0e8b032e`; old 3 red, corrected 18/18 diagnostic controls, root ownership integration 20/20 |
| R08 | Explicitly seeded SP can bypass the multi-floor candidate bank (A35) | Slice pass 2026-11-14 | `77d9c7b2`; root old 1 red/1 positive; corrected lower controls 16/16, CPU consumers 2/2 and Equilibrium 2197/2197; final integrated hardware matrix pending |
| R09 | A cumulative sampled-run time is used as a single-launch safety observation | Final native pass 2026-11-15; GitHub acceptance separate | Actual synchronized launch count, coverage and maximum are distinct from aggregate time. C1 `c1db01d3` budget/calibration/P1 gate 5/5: KO2 maximum 436.6268 ms and trace-gas maximum 80.4482 ms, both below the unchanged 500 ms target; 16,384 cases covered in each workload. Earlier cumulative-time failures remain in the external receipts. Final c0df maxima: Windows KO2/trace 454.293/86.7546 ms; Linux 422.3941/77.4743 ms; complete 16,384-case coverage and actual 1/15/1 cold/hit/changed calibration each. |
| R10 | Candidate-bank changes regress approved kernel throughput | Final native pass 2026-11-15; GitHub acceptance separate | Typed per-case state storage, private crossing code-generation boundary and bounded calibration of identical workloads are integrated in `c1db01d3`. Original quiet throughput retry passes: median 1.306e-7 s/Newton step below the unchanged 1.42255e-7 ceiling; cold sample retained. The initial same-code failure (1.469e-7), older failures and diagnostic-only measurements remain recorded. No threshold or throughput approval changed. Final c0df original quiet facts pass first attempts on both platforms: medians 1.269e-7 Windows and 1.254e-7 Linux s/Newton step, with cold samples retained; unchanged ceilings 1.42255e-7 Windows and 1.42025e-7 Linux. |
| R11 | Invalid approved throughput scalars can disable a regression gate | Final native pass 2026-11-15; GitHub acceptance separate | `3883976c`, `d928da01`; `ThroughputRecordTests`: old per-step reader 9 red/18 green, old ratio reader 12 red/33 green; corrected combined controls 45/45. Both approved scalars require finite positive values; per-step measurements require finite nonnegative values. Original inclusive 5x, 80% and 115% arithmetic is preserved, with representable-boundary controls. Wall-time messages no longer infer a code-only cause. Both final c0df Release CPU cohorts pass, including the finite approved-scalar/ratio and representable-boundary controls; no throughput approval changed. |
| R12 | Host calibration integration assumes a fixed adaptive launch partition | Final native pass 2026-11-15; GitHub acceptance separate | `96f1bcaf`; the full Windows CPU run at `a56ba02b` passed 7,609/7,610; the sole failure expected first block 15 instead of the legitimate adaptive 10. Controlled observation histories now drive the actual batch loop: both 15 and 10 are exact positive controls, independent of host scheduling. The real cold-input-to-changed-input path remains; post-download-key and skipped-current-work mutations fail. Corrected loop/prefix controls 15/15; no production, GPU assertion, budget or approval changes. Both final c0df Release CPU cohorts pass 7,611/7,611, including controlled prefix 15 and non-doubling 10 and the preserved real cold-to-changed-input path. |

## Integration verification

The actual GitHub runs after local acceptance exposed two additional delivery/test
issues. They remain part of this closure, without reclassifying failed runs as passes.

| ID | Mechanism | Status | Evidence |
|---|---|---|---|
| R13 | Floating CI Python selects an ABI without a published CEA wheel | Local fix and hosted binding step pass 2026-11-15; whole CI pending | CI `43687811351` at `f7692b80` selected Python 3.15.2, built CEA 3.3.4 from source and failed native import before fixture comparison. `d4540f3b` selects Python 3.13 x64, requires binary dependencies and checks native import before the unchanged sample binding. Fresh Windows import and 38 comparisons pass; the corresponding hosted Windows step in CI `43689713770` now passes. No CEA model, fixture, tolerance or approval changed |
| R14 | Linux process liveness can report an exited zombie as a surviving child | Local pass 2026-11-15; hosted confirmation pending | Release `43687839467` at `f7692b80` failed the tree-cleanup assertion, while CI Ubuntu passed the same 7,611 cases. The historical log contains no kernel-state sample. A controlled owned zombie deterministically reproduces `Process.HasExited=false`; `b0c11e30` observes Linux process state and keeps sleeping/stopped processes live. Direct-parent-only kill mutations fail both tree controls on both OSes; final Fixtures.Tests passes 188/188 on each. Harness, probe, deadlines, raw bytes and all numerical materials are unchanged |

- [x] Strict protocol lint and all tool self-tests.
  Evidence 2026-11-15: final native proof at executed code `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`;
  the dated final native verification table below names the source/result/TRX
  and supplementary evidence. GitHub validation remains a separate open criterion.
- [x] Build with zero warnings/errors; reflection, signatures, dependencies and shape.
  Evidence 2026-11-15: final native proof at executed code `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`;
  the dated final native verification table below names the source/result/TRX
  and supplementary evidence. GitHub validation remains a separate open criterion.
- [x] Targeted public regressions and independent numerical witnesses.
  Evidence 2026-11-15: final native proof at executed code `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`;
  the dated final native verification table below names the source/result/TRX
  and supplementary evidence. GitHub validation remains a separate open criterion.
- [x] Windows and Linux CPU suites, including process examples and long scans.
  Evidence 2026-11-15: final native proof at executed code `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`;
  the dated final native verification table below names the source/result/TRX
  and supplementary evidence. GitHub validation remains a separate open criterion.
- [x] Serialized Windows/WSL CUDA correctness, bit records and throughput.
  Evidence 2026-11-15: final native proof at executed code `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`;
  the dated final native verification table below names the source/result/TRX
  and supplementary evidence. GitHub validation remains a separate open criterion.
- [x] Local packages, samples, installed tool, schemas and symbols.
  Evidence 2026-11-15: final native proof at executed code `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`;
  the dated final native verification table below names the source/result/TRX
  and supplementary evidence. GitHub validation remains a separate open criterion.
- [x] Workflow validation on the affected runner paths.
  (2026-11-15: Exact-source `0f7ecb6f4fff` CI `43691428901`, hosted trace
  `43691604795` and Release dispatch `43691442012` all pass; both CUDA/preflight
  jobs and Pack succeed, Publish and GitHub Release are skipped. Native c0df
  evidence and explicit unchanged-runtime comparison retain their actual sources.
  External audit index: `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.)
  (2026-11-15: exact source `f21ac451c286`. Actual CI, Trace, both GPU jobs, Pack and
  PDB/SourceLink checks pass.
  CI `43742270400`, Trace `43742270272`, Release `43742272942`.
  Source-bound reports, controls and artifact inventory:
  external `fix-evidence/github-f21a/final-proof-index-f21a-v1.json`.)

## Final native verification — 2026-11-15

Executed code: `c0df7b146ffdd3addb733e8931aaabf0ca5fc660`. Receipt paths below
are relative to the external audit's `fix-evidence/`. A later document-only head
must carry an explicit unchanged-code comparison; execution and SourceLink records
retain c0df as their actual source. These local results do not establish GitHub
runner execution, and the workflow criterion and root aggregate stay unchecked.

| Proof | Windows | Ubuntu-24.04 | External receipts |
|---|---|---|---|
| Release build | 0 warnings/errors | 0 warnings/errors | `final-candidate/c0df7b146ffd-windows-build/result.json`; `final-candidate/c0df7b146ffd-linux-build/result.json` |
| Full CPU solution | 7,611 passed, 0 skipped | 7,611 passed, 0 skipped | `final-candidate/c0df7b146ffd-windows-cpu/`; `final-candidate/c0df7b146ffd-linux-cpu/` |
| Full CORE-MATH | 1 fact over 2,266,122 pinned inputs | same full corpus, 1 passed fact | `final-candidate/c0df7b146ffd-windows-math/`; `final-candidate/c0df7b146ffd-linux-math/` |
| TraceGas scans | exact 2/2, both full corpora | exact 2/2, both full corpora | `final-candidate/c0df7b146ffd-windows-trace/`; `final-candidate/c0df7b146ffd-linux-trace/` |
| Actual budget/P1/calibration | 5/5; KO2/trace max 454.293/86.7546 ms | 5/5; max 422.3941/77.4743 ms | `final-candidate/c0df7b146ffd-windows-budget-phase/`; `final-candidate/c0df7b146ffd-linux-budget-phase/` |
| Original quiet throughput | median 1.269e-7 s/Newton step | median 1.254e-7 s/Newton step | `final-candidate/c0df7b146ffd-windows-throughput/`; `final-candidate/c0df7b146ffd-linux-throughput/`; first cold samples retained, all original gates unchanged |
| Full CUDA science/bits/architecture | 449/449 passed | 449/449 passed | `final-candidate/c0df7b146ffd-windows-cuda/`; `final-candidate/c0df7b146ffd-linux-cuda/`; actual device and unchanged source/binary records |
| Compile and release resources | all 3 RocketCompileTests passed | all 3 passed | same Release CPU TRX: allocations 5,221,920,496/5,223,287,992 B below 7 GiB; weak launcher/program release controls pass |
| Debug build / fast / end-to-end | 0 warnings/errors; 7,580 in 179.415 s; 32 in 195.466 s | Release platform proof above | `final-candidate/c0df7b146ffd-windows-debug/result.json`; original 300/600 s limits |
| Tools and protocol | 10 original processes exit 0, 281 methods | applicable portable controls in CPU/scan lanes | `final-candidate/c0df7b146ffd-windows-tools-revalidated-parser-v2/result.json`; metadata parser correction, no execution rerun; original adapter exit 3 preserved |
| Fresh package consumption and symbols | 12 exact samples; 11 installed-tool checks (7 real, 4 cache); library 7 / tool 8 PDBs all c0df SourceLink | no package-platform claim added | `package-final-c0df7b14/final-package-receipt.json` and `pdb-final-proof-v1/result.json`; isolated feed/cache SHA-512 and all 4 archive hashes |

The versioned `final-candidate/supplementary-proof-index-c0df7b146ffd-v1.json`
links completed Debug, original tool execution/revalidation and package/PDB proof.
Earlier C0 failures, the first c1db throughput failure and a56's 7,609/7,610 CPU
cohort remain historical failures, not environment passes or c0df executions.

⚠ 2026-11-15: the following complete prior R09-R12/T10 rows are retained as the
pre-final-native handoff state. Their pending statements are superseded by the
source-pinned native acceptance above; GitHub acceptance is still open.

> | R09 | A cumulative sampled-run time is used as a single-launch safety observation | Local hardware pass 2026-11-15; full matrix pending | Actual synchronized launch count, coverage and maximum are distinct from aggregate time. C1 `c1db01d3` budget/calibration/P1 gate 5/5: KO2 maximum 436.6268 ms and trace-gas maximum 80.4482 ms, both below the unchanged 500 ms target; 16,384 cases covered in each workload. Earlier cumulative-time failures remain in the external receipts |
> | R10 | Candidate-bank changes regress approved kernel throughput | Local hardware pass 2026-11-15; full matrix pending | Typed per-case state storage, private crossing code-generation boundary and bounded calibration of identical workloads are integrated in `c1db01d3`. Original quiet throughput retry passes: median 1.306e-7 s/Newton step below the unchanged 1.42255e-7 ceiling; cold sample retained. The initial same-code failure (1.469e-7), older failures and diagnostic-only measurements remain recorded. No threshold or throughput approval changed |
> | R11 | Invalid approved throughput scalars can disable a regression gate | Local pass 2026-11-15; integrated matrix pending | `3883976c`, `d928da01`; `ThroughputRecordTests`: old per-step reader 9 red/18 green, old ratio reader 12 red/33 green; corrected combined controls 45/45. Both approved scalars require finite positive values; per-step measurements require finite nonnegative values. Original inclusive 5x, 80% and 115% arithmetic is preserved, with representable-boundary controls. Wall-time messages no longer infer a code-only cause |
> | R12 | Host calibration integration assumes a fixed adaptive launch partition | Local pass 2026-11-15; integrated matrix pending | `96f1bcaf`; the full Windows CPU run at `a56ba02b` passed 7,609/7,610; the sole failure expected first block 15 instead of the legitimate adaptive 10. Controlled observation histories now drive the actual batch loop: both 15 and 10 are exact positive controls, independent of host scheduling. The real cold-input-to-changed-input path remains; post-download-key and skipped-current-work mutations fail. Corrected loop/prefix controls 15/15; no production, GPU assertion, budget or approval changes |
> | T10 | Distinguish CPU-forbidden CUDA facts from real hardware proof | Separate final hardware lanes; explicit backend/device reporting and new P1 consumers in progress |

## Test-quality review obligations

T01–T06 map to the original numerical, comparison, corpus, ownership and protocol
findings above; T09 maps to A38. The remaining recommendations are tracked explicitly
so that correcting a test name cannot silently substitute for its behavioral check.

| Review | Obligation | Status and evidence |
|---|---|---|
| T07 | Observe compilation and release without memory floors | `b5b72b23`; warm-up/cache/weak-reference observations, upper budgets retained; cache/IR retention mutations red |
| T08 | Separate family results from fallback-path inference | `b15c6cdd`; behavior and mechanism facts split, 13/13; marker mutation fails five diagnostics while five result checks remain green |
| T10 | Distinguish CPU-forbidden CUDA facts from real hardware proof | 2026-11-15: source-pinned CPU lanes and actual device lanes pass separately; budget/P1/calibration 5/5 each, full Windows/Ubuntu CUDA and original quiet throughput pass. Forbidden-CUDA CPU returns are not hardware proof |
| T11/T12 | Retain independent scientific evidence and bounded bit contracts | Existing independent suites retained; one BeO numerical row approved only after raw NASA9 and independent sonic validation (`0702dd45`, `effc89f2`) |
| T13 | Show solved-throat sensitivity to skipped tail execution | `508ab286`; analytic Ar calibration and independently integrated/bisected N2 sonic state, skipped-tail mutation fails the physical comparison while policy/Ar pass; Performance 1496/1496, no production/tolerance/snapshot change |
| T14 | Bound subprocess completion and preserve partial output | `af3ac921`; root process-fixture integration 79/79, controlled timeout/cancellation/pipe cases |
| T15 | Add a literal independent mass-tolerance validity table | `4dcb374b`; finite boundaries, NaN and both infinities, public boundary controls retained |
| T16 | Print actual named fields for numerical bit mismatches | Thermo, Equilibrium, Performance, Transport, Problems and CLI callers updated; one-ULP mutations remain red without approved-record changes |

## Review recommendations

Apply concrete hardening where it preserves the approved model: process deadlines,
diagnostic field differences, explicit accelerator provenance and data-format checks.
Keep independent science oracles, backend bit checks and policy tests in their
distinct roles. A numerical refusal is not proof of nonexistence; differing Air inputs
are not an equal-input parser error. CUDA timing headroom is measured without provoking
a display-driver reset. Changes to model domains or unbounded cache policy need an
explicit contract, not an invented defect or an unexplained default change.

## Final GitHub verification — 2026-11-15

Actual source: `0f7ecb6f4fff173fc8716e93655303f09b412d7c`, branch
`codex/audit-closure`. These actual runner-path results supplement, rather than
redate, the native c0df measurements above.

| Path | Actual result | Source-bound evidence |
|---|---|---|
| CI Windows / Linux | 7,612/7,612 each, zero failed or skipped; package samples and installed tool pass | run `43691428901`; Windows native CEA import and unchanged 38-case binding pass |
| Hosted trace Windows / Linux | 2/2 full-corpus scan facts each, zero skipped | dispatch `43691604795`; discovery/matrix/TRX ZIP digests verified |
| Release hosted Windows / Linux | 7,612/7,612 each, Fixtures 188/188, zero failed or skipped | dispatch `43691442012`, jobs `132742795768` / `132742795725` |
| Release CUDA Windows / Linux | 450/450 each, zero failed or skipped; original throughput fact included | jobs `132747447148` / `132748505091`; actual matching preflight scripts and runner labels |
| Original throughput | Windows 34.26x and 1.314e-7 s/step; Linux 48.64x and 1.272e-7 s/step | Fresh host `Throughput.actual.txt` / `Throughput.linux.actual.txt`; unchanged 5x/80%/115% gates and distinct original ceilings |
| Release packages / symbols | Pack passes; all 4 archive digests and commit metadata checked; library 7 PDB / 212 documents, tool 8 PDB / 283 documents, SourceLink records all name exact 0f7 | artifact `14153576112`; compiled read-only PDB verifier, no repeated pack/sample execution |
| Temporary GPU listeners | Both owned run-once processes ended; no runner service created | external Windows/Linux terminal receipts; other repository runners untouched |

The final proof index records every source/run/job/artifact and supporting hash.
GitHub server job timestamps and runner-host log/file clocks are retained separately;
the release dependency graph establishes Windows-before-Linux ordering. A clock
comparison across those domains is not used as a scheduling or freshness proof.
