# Audit closure — 2026-11-14

Baseline: `1cfbaea8d1d75af670aebfb456e05702e72ec533`.
The owner requested closure of the combined Sol, independent second-pass and Claude
findings, plus justified improvements from the test-quality review. The external audit
retains the original reproductions; these stable IDs preserve the mapping.

## Acceptance

A row closes only after its old-failing/positive controls and affected checks pass.
Document-only corrections use an addressable contract comparison instead of synthetic
tests. The final integrated CPU, CUDA, platform and package checks remain separate.
Historical measurements and active criteria are preserved; no blind snapshot approval,
tolerance weakening or diagnostic suppression is an acceptable closure.

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
| R09 | A cumulative sampled-run time is used as a single-launch safety observation | Local hardware pass 2026-11-15; full matrix pending | Actual synchronized launch count, coverage and maximum are distinct from aggregate time. C1 `c1db01d3` budget/calibration/P1 gate 5/5: KO2 maximum 436.6268 ms and trace-gas maximum 80.4482 ms, both below the unchanged 500 ms target; 16,384 cases covered in each workload. Earlier cumulative-time failures remain in the external receipts |
| R10 | Candidate-bank changes regress approved kernel throughput | Local hardware pass 2026-11-15; full matrix pending | Typed per-case state storage, private crossing code-generation boundary and bounded calibration of identical workloads are integrated in `c1db01d3`. Original quiet throughput retry passes: median 1.306e-7 s/Newton step below the unchanged 1.42255e-7 ceiling; cold sample retained. The initial same-code failure (1.469e-7), older failures and diagnostic-only measurements remain recorded. No threshold or throughput approval changed |
| R11 | Invalid approved throughput scalars can disable a regression gate | Local pass 2026-11-15; integrated matrix pending | `3883976c`, `d928da01`; `ThroughputRecordTests`: old per-step reader 9 red/18 green, old ratio reader 12 red/33 green; corrected combined controls 45/45. Both approved scalars require finite positive values; per-step measurements require finite nonnegative values. Original inclusive 5x, 80% and 115% arithmetic is preserved, with representable-boundary controls. Wall-time messages no longer infer a code-only cause |
| R12 | Host calibration integration assumes a fixed adaptive launch partition | Local pass 2026-11-15; integrated matrix pending | `96f1bcaf`; the full Windows CPU run at `a56ba02b` passed 7,609/7,610; the sole failure expected first block 15 instead of the legitimate adaptive 10. Controlled observation histories now drive the actual batch loop: both 15 and 10 are exact positive controls, independent of host scheduling. The real cold-input-to-changed-input path remains; post-download-key and skipped-current-work mutations fail. Corrected loop/prefix controls 15/15; no production, GPU assertion, budget or approval changes |

## Integration verification

- [ ] Strict protocol lint and all tool self-tests.
- [ ] Build with zero warnings/errors; reflection, signatures, dependencies and shape.
- [ ] Targeted public regressions and independent numerical witnesses.
- [ ] Windows and Linux CPU suites, including process examples and long scans.
- [ ] Serialized Windows/WSL CUDA correctness, bit records and throughput.
- [ ] Local packages, samples, installed tool, schemas and symbols.
- [ ] Workflow validation on the affected runner paths.

## Test-quality review obligations

T01–T06 map to the original numerical, comparison, corpus, ownership and protocol
findings above; T09 maps to A38. The remaining recommendations are tracked explicitly
so that correcting a test name cannot silently substitute for its behavioral check.

| Review | Obligation | Status and evidence |
|---|---|---|
| T07 | Observe compilation and release without memory floors | `b5b72b23`; warm-up/cache/weak-reference observations, upper budgets retained; cache/IR retention mutations red |
| T08 | Separate family results from fallback-path inference | `b15c6cdd`; behavior and mechanism facts split, 13/13; marker mutation fails five diagnostics while five result checks remain green |
| T10 | Distinguish CPU-forbidden CUDA facts from real hardware proof | Separate final hardware lanes; explicit backend/device reporting and new P1 consumers in progress |
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

