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
| A04 | Rollback partial table allocation | In progress | `233c8cca`, `395ef758` cover table/wrapper transactions; independent copy-boundary and diagnostic-storage follow-ups R06/R07 remain |
| A05 | Exact condensed-set identity | Local pass 2026-11-14 | `14d875bb`; Performance integration controls 32/32; architecture follow-up in progress |
| A06 | Ordinary-throat reference sensitivity | Local pass 2026-11-14 | `14d875bb`; finite corrupted ordinary-throat controls included in 32/32 |
| A07 | Finite transport comparison | Local pass 2026-11-14 | `14d875bb`; Transport integration controls 73/73 |
| A08 | Benchmark comparison rejects nonfinite mismatches | Local pass 2026-11-14 | `ed8acaae`; whole-entry controls included in root 90/90 contract/comparison run |
| A09 | Actual boundary-walk cap verification | Local pass 2026-11-14 | `14d875bb`; actual automaton cap and mutation controls; root Performance 32/32 |
| A10 | First-maximum ordering oracle | Local pass 2026-11-14 | `14d875bb`; late-higher-peak rejection; root Performance 32/32 |
| A11 | Generated performance field inventory | Local pass 2026-11-14 | `14d875bb`; reflected six-field corruption controls; root Performance 32/32 |
| A12 | Pinned full math corpus integrity | Slice pass 2026-11-14 | `7d42fae6`; seven portable corpus refusal controls; pinned full identity verified; root full rounding campaign in progress |
| A13 | Internal frozen-state finite guards | Local pass 2026-11-14 | `4755241d`, `77afac60`; root finite-state/bit controls 17/17, representable extreme positive cases retained |
| A14 | Internal transport finite temperature | Local pass 2026-11-14 | `14d875bb`; root Transport controls 73/73 |
| A15 | Windows and Linux CUDA-driver detection | Slice pass 2026-11-14 | `17f048d2`; shared name predicate and ordinary/forbidden-module controls; final platform process checks pending |
| A16 | Approved snapshot write protection | Local pass 2026-11-14 | `200a1bc2`; root Fixtures 68/68, including ordinary/link output controls |
| A17 | Schema validator semantics and keyword coverage | Local pass 2026-11-14 | `200a1bc2`; root Fixtures 68/68 and schema consumers 3/3 |
| A18 | Chunk enumerator integer boundary | Slice pass 2026-11-14 | `17f048d2`; finite int.MaxValue covering and ordinary/empty chunks in boundary controls; root Protocol 131/131 |
| A19 | Pinned and atomically published merge candidate | Local pass 2026-11-14 | `a6ff702c`; merge guard self-tests 72/72, independent rerun |
| A20 | Preservation of active acceptance criteria | Local pass 2026-11-14 | `a6ff702c`; history guard 35/35 and baseline-to-HEAD criteria check |
| A21 | Complete documented signatures | Local pass 2026-11-14 | `3b9f5f7e`, `739a69b4`; root contract/comparison controls 90/90; final changed-document reconciliation pending |
| A22 | Complete source and build-manifest node discovery | Local pass 2026-11-14 | `7ec349d6`; root lint self-tests 84/84 and strict lint 0/0 |
| A23 | PowerShell preflight failure propagation | Local pass 2026-11-14 | `4e5d1494`, `a3191c24`; root guards 20/20, real Windows PS5.1 and Ubuntu-24.04 preflight pass; GitHub runner proof pending |
| A24 | Current protocol-linter negative controls | Local pass 2026-11-14 | `a6ff702c`; lint self-tests 75/75, strict lint 0/0 |
| A25 | Inclusive sweep limit | Local pass 2026-11-14 | `e8b9bf46`, `695d9715`; decimal maximum regression corrected after independent review; root CLI CPU 177/177 (`fix-evidence/root/cli-integration-current.trx`) |
| A26 | Input/output schema parity | Slice pass 2026-11-14 | `6bf771aa`; conditional shape controls 42/42, real zero-mass InvalidInput output admitted; root CLI 177/177; docs schema approval pending |
| A27 | Finite fixture tolerances | Local pass 2026-11-14 | `ed8acaae`; finite-intermediate-overflow control included in root Fixtures 68/68 |
| A28 | Current contract and guide claims | Local pass 2026-11-14 | Owning numerical/public claims corrected, `9dd497c9` benchmark launch/fallback/setup scope, `8b5cbfa7` exact sample snippet, `9ea4c213` cleanup scope; final docs consumer run pending |
| A29 | Unambiguous chemical-system cache key | Local pass 2026-11-14 | `782ced8f`; framed-name collision and positive controls in root public 28/28 |
| A30 | Release propellant references at Dispose | Local pass 2026-11-14 | `782ced8f`; reachability and cleanup-failure controls in root public 28/28 |
| A31 | Scale-invariant group normalization | Local pass 2026-11-14 | `782ced8f`; subnormal group control in root public 28/28; ordinary records unchanged |
| A32 | Complete batch-composition comparison | Local pass 2026-11-14 | `31c28741`; union/condensed/zero-key controls in root public 28/28 |
| A33 | Literal release tag shell arguments | Local pass 2026-11-14 | `4e5d1494`; adversarial valid-ref and fake-gh controls in root 20/20; affected GitHub runner proof pending |
| A34 | Literal changelog version matching | Local pass 2026-11-14 | `4e5d1494`; exact/prerelease/build-metadata, lookalike and empty-section controls in root 20/20 |
| A35 | Multi-floor HP/SP objective selection | Local pass 2026-11-14 | `b8f90090`; complete bank/floor/independent-witness controls 34/34 in integration; hardware/public witness matrix pending |
| A36 | Failed-state public postcondition | Local pass 2026-11-14 | `14d875bb`, `782ced8f`; internal/public failure controls, NoGasPhase exception and prior-success preservation |
| A37 | Public condensed-model applicability | Local pass 2026-11-14 | `668939f6`, `14d875bb`; Problems contract and rocket/troubleshooting guides name frozen extrapolation and retained phase bounds |
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
| R02 | Nonfinite conversion bypass in JSON field comparison | In progress | Independent IT01: approved 1 versus delivered 1e400 is incorrectly accepted |
| R03 | Binary64 rounding in schema numeric decisions (A17) | In progress | Independent IT02: fractional integer, adjacent large bounds and tiny minima |
| R04 | Explicitly false documented base/interface contracts (A21) | In progress | Independent IT03: nonexistent IDisposable/Exception bases accepted |
| R05 | URI-fragment and malformed JSON Pointer resolution (A17) | In progress | Independent IT04: percent-encoded ref chooses a different schema |
| R06 | Copy failure before allocation result enters ownership (A04) | In progress | Actual ILGPU 1.5.3 CPU-buffer control; Species, Transport and ProbeMath convenience uploads |
| R07 | Transport diagnostic storage can replace the primary (A04) | In progress | Read-only Exception.Data throws NotSupportedException; original failure must survive |

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
| T08 | Separate family results from fallback-path inference | In progress; `tests/Execution.Tests/BOOT.md` records the diagnostic scope |
| T10 | Distinguish CPU-forbidden CUDA facts from real hardware proof | Separate final hardware lanes; explicit backend/device reporting and new P1 consumers in progress |
| T11/T12 | Retain independent scientific evidence and bounded bit contracts | Existing independent suites retained; one BeO numerical row approved only after raw NASA9 and independent sonic validation (`0702dd45`, `effc89f2`) |
| T13 | Show solved-throat sensitivity to skipped tail execution | In progress; `tests/Performance.Tests/BOOT.md` requires an independent sonic reference and a red skipped-tail mutation |
| T14 | Bound subprocess completion and preserve partial output | `af3ac921`; root process-fixture integration 79/79, controlled timeout/cancellation/pipe cases |
| T15 | Add a literal independent mass-tolerance validity table | `20688e9b`; finite boundaries, NaN and both infinities, public boundary controls retained |
| T16 | Print actual named fields for numerical bit mismatches | Thermo, Equilibrium, Performance, Transport, Problems and CLI callers updated; one-ULP mutations remain red without approved-record changes |

## Review recommendations

Apply concrete hardening where it preserves the approved model: process deadlines,
diagnostic field differences, explicit accelerator provenance and data-format checks.
Keep independent science oracles, backend bit checks and policy tests in their
distinct roles. A numerical refusal is not proof of nonexistence; differing Air inputs
are not an equal-input parser error. CUDA timing headroom is measured without provoking
a display-driver reset. Changes to model domains or unbounded cache policy need an
explicit contract, not an invented defect or an unexplained default change.

