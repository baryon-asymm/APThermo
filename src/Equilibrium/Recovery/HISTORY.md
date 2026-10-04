# HISTORY.md — src/Equilibrium/Recovery

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="recheck-ok"></a>

## 2026-10-04 — from "## Invariants" — an Ok attempt ends the case untouched

Coder 2 of 0.2.2, the addendum of the no-ice investigation: a cold hp or sp attempt that ends `Ok` below a dead-end floor is rechecked (`## Constraints`, "Dead-end floors"). The wording of the invariant stood:

> - **An `Ok` attempt ends the case untouched.** The plan acts only after a failure: a case whose
>   first attempt or cold fallback ends `Ok` ends with the bits it had before 0.2.2.
