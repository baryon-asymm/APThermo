# BOOT.md — .github/workflows

## Purpose

GitHub event and job orchestration for hosted verification and approved releases.

## Invariants

Pull requests use hosted runners only. Release ref names enter Bash through quoted environment variables. Hosted CPU trace scans run on affected pushes/PRs and manual dispatch; no nightly job is claimed.

## Dependencies

[delivery](../API.md)
[scripts](../scripts/API.md)
[preflight](../actions/preflight/API.md)
[Equilibrium.Tests](../../tests/Equilibrium.Tests/API.md)

Outside the tree: host shells and GitHub Actions versions inherited from the ancestor.

## Constraints

Inherit .github delivery policy and hosted evidence requirement. Every changed path must have executable local guards and a later actual GitHub run on its runner class. Trace scan discovery must contain both scan facts before execution; upload TRX and diagnostic files even after failure.

CI's Windows fixture binding uses CPython 3.13 x64, a published-wheel ABI for the pinned
CEA 3.3.4 reference package. Install the existing pinned requirements binary-only
in a fresh virtual environment and import the native binding before running the
unchanged generator sample comparison. Missing compatible wheels or native DLLs
fail the job; never fall back to a local Fortran build or skip fixture binding.

Hosted CI and release matrices use OS × lane(core/cli/docs). Each calls the one
scripts-node hosted test runner. Artifact names include OS and lane to avoid
collisions. CI Pack/sample/tool and Windows CEA binding run only in core; package
upload remains Ubuntu core. Release hosted job timeout stays thirty minutes per
lane and CUDA/publishing dependency/event conditions remain unchanged.

R16 requires `-m:1` in each GPU solution test command. Keep the existing shell,
Release/no-build and exact `Category=Cuda|Category=BitSnapshot` filter. Preserve
the shared Execution.Tests process, original warm-ups, all three timed samples,
calibration TTL, gates, Windows-before-Linux dependency and job limits. Emit fresh
TRX per project and always upload GPU evidence under an OS/run/attempt-unique
artifact name; do not introduce Python or another self-hosted dependency. The
former concurrent command remains a historical failed run, not proof of its cause.

## Acceptance criteria

- [ ] R16: both self-hosted GPU jobs execute solution test projects sequentially,
  preserve the complete original CUDA-or-bit selection and all three timed samples,
  and pass unchanged throughput and launch-budget gates on Windows and Linux.
  Frozen former commands are red under the new bounded wiring guard; deliberate
  loss of serialization or selected work is refused. Fresh source-bound reports
  and actual job logs establish the executed cases, no skips and project ordering;
  Pack passes, while dispatch publishing/release creation remain skipped.

- [x] 2026-11-15 — R15 local wiring: `test_hosted_tests.py` passes 15 tests,
  including deliberate lane/filter/package/binding/artifact/timeout/dependency
  mutations; actionlint 1.7.12 accepts CI/release. Release bytes from cuda-windows
  onward match the frozen pre-change source exactly. Evidence:
  `SCRATCH/audit/hosted-test-lanes/hosted-green.txt`, `actionlint.txt` and
  `frozen-controls.json` in the same directory. Actual hosted evidence stays open.


- [ ] R15 workflow guards reject missing/duplicate lane wiring, dropped filters,
  misplaced package/binding steps or colliding artifacts; actual hosted CI and
  release dispatch cover all lanes on both OS with complete source-bound evidence.


- [x] 2026-11-14 — A39 local wiring: eight `test_trace_scan.py` guards pass; bypassing
  discovery verification produces four red subcases, bypassing TRX verification
  produces seven. The actual built Debug assembly discovers exactly both documented
  CPU/LongRunning facts (`SCRATCH/audit/trace-discovery/matrix.json`); that matrix
  explicitly says not-run. actionlint 1.7.12 accepts the affected workflow files.
- [x] A real hosted Windows/Linux trace-scan run executes both full scan corpora and
  publishes discovery/matrix/TRX evidence with no skip. The audit baseline is 4158
  meaningful scan rows and 1632 expected states; each owning scan fact enforces its
  corpus. Local metadata discovery is not execution proof.
  (2026-11-15: Trace dispatch `43691604795` at exact `0f7ecb6f4fff` executes
  both full corpora, exactly 2 passed facts per hosted OS, with discovery/matrix/TRX
  artifact digests verified. CI/Release also pass the actual delivery and trace guards.
  External audit index: `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.)

- [x] 2026-11-14 — A33 local controls: both release ref uses bind through the actual
  YAML env mappings and quoted shell variables (`test_delivery.py`, 11 total guards);
  valid Git refs containing quotes, semicolons and command substitution remain one
  literal value. The frozen old workflow executes their controlled sentinel and
  turns the guards red. actionlint 1.7.12 reports no workflow errors.

- [x] A33/A39: adversarial ref values remain literal, hosted scan wiring discovers both facts and publishes nonempty evidence; a real hosted run remains required.
  (2026-11-15: Trace dispatch `43691604795` at exact `0f7ecb6f4fff` executes
  both full corpora, exactly 2 passed facts per hosted OS, with discovery/matrix/TRX
  artifact digests verified. CI/Release also pass the actual delivery and trace guards.
  External audit index: `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.)

- [x] 2026-11-15 — Isolated Windows CPython 3.13.15, CEA 3.3.4 and NumPy 2.5.3
  binary wheels load libcea with isolated mode and empty PATH; full unchanged
  generator sample reports unchanged 38. Evidence: SCRATCH/audit/install-cea313.log,
  native-import-cea313.log and fixture-sample-cea313.log. The binary-only cp315
  download refuses the unavailable wheel (cp315-no-wheel-red.log).
- [x] The changed Python environment and mandatory binding comparison pass on a
  real hosted Windows run; local import/sample evidence is not hosted proof.
  (2026-11-15: CI `43691428901`, hosted Windows job `132742713874`, passes
  isolated CPython 3.13.17 x64 / CEA 3.3.4 / NumPy 2.5.3 native import and the
  unchanged 38-fixture sample comparison at exact source `0f7ecb6f4fff`.
  External audit index: `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.)

## Taboos

Do not run PR code on self-hosted runners or claim local validation proves GitHub execution.

⚠ 2026-11-14: two release steps formerly embedded github.ref_name into generated
Bash source. They now pass RELEASE_REF_NAME as environment data; event/version
inputs use the same boundary. Strict protocol lint and the delivery guard suite
now run explicitly in hosted CI/release matrices. These are local/CI guard changes;
actual GitHub execution is still required by the ancestor's evidence invariant.

⚠ 2026-11-15: CI selected floating Python 3.x (3.15.2 on hosted Windows job
132731873556). CEA 3.3.4 had no cp315 wheel, so pip built an sdist successfully
but importing libcea failed with a missing DLL before fixture comparison. The
candidate environment uses the published cp313 wheel with binary-only installation;
isolated Windows import and sample comparison of 38 fixtures now pass locally; actual hosted Windows CI is still required.
Primary environment evidence: [CEA3.3.4 release metadata](https://pypi.org/pypi/cea/3.3.4/json)
records the cp313 Windows wheel and no cp315 wheel; [NASA installation guidance](https://github.com/nasa/cea#install-recommended)
distinguishes published-package installation from source builds. The missing DLL
of the hosted source build is not identified by its ImportError; no stronger root
cause is claimed. The tested wheel carries the C binding DLL within its package.

⚠ 2026-11-15: the pending hosted-import and scan phrases above record the local
handoff stage. The dated criteria now cite successful actual hosted import/binding,
full trace corpora and the complete release dispatch at exact source `0f7ecb6f4fff`.
