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

## Acceptance criteria

- [x] 2026-11-14 — A39 local wiring: eight `test_trace_scan.py` guards pass; bypassing
  discovery verification produces four red subcases, bypassing TRX verification
  produces seven. The actual built Debug assembly discovers exactly both documented
  CPU/LongRunning facts (`SCRATCH/audit/trace-discovery/matrix.json`); that matrix
  explicitly says not-run. actionlint 1.7.12 accepts the affected workflow files.
- [ ] A real hosted Windows/Linux trace-scan run executes both full scan corpora and
  publishes discovery/matrix/TRX evidence with no skip. The audit baseline is 4158
  meaningful scan rows and 1632 expected states; each owning scan fact enforces its
  corpus. Local metadata discovery is not execution proof.

- [x] 2026-11-14 — A33 local controls: both release ref uses bind through the actual
  YAML env mappings and quoted shell variables (`test_delivery.py`, 11 total guards);
  valid Git refs containing quotes, semicolons and command substitution remain one
  literal value. The frozen old workflow executes their controlled sentinel and
  turns the guards red. actionlint 1.7.12 reports no workflow errors.

- [ ] A33/A39: adversarial ref values remain literal, hosted scan wiring discovers both facts and publishes nonempty evidence; a real hosted run remains required.

## Taboos

Do not run PR code on self-hosted runners or claim local validation proves GitHub execution.

⚠ 2026-11-14: two release steps formerly embedded github.ref_name into generated
Bash source. They now pass RELEASE_REF_NAME as environment data; event/version
inputs use the same boundary. Strict protocol lint and the delivery guard suite
now run explicitly in hosted CI/release matrices. These are local/CI guard changes;
actual GitHub execution is still required by the ancestor's evidence invariant.
