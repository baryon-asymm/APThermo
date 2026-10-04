# HISTORY.md — src/Equilibrium/Recovery

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="magnesite-band-2026-10-04"></a>

## 2026-10-04 — from "## Acceptance criteria" — the declared magnesite band

The trace-gas pass reaches the states the third criterion of "Bracketed `Ok`" declared left. The text stood:

> Left, and declared: magnesite at 1e5 Pa seeded from the one-condensed side ends
> `TemperatureOutOfRange` after the bracket (the thin band of a trace gas, the trace-gas
> coder's; the prototype's own scan gave the same 97 to 123 iterations).

---

<a id="tracegas-seams-2026-10-04"></a>

## 2026-10-04 — from "## Constraints" — the trace-gas seam

Coder 3 of 0.2.2 filled the seam with the trace-gas node ([TraceGas](../TraceGas/BOOT.md)). The bullet, as coder 2 left it,
stood:

> - **The trace-gas seam** (its own design, 0.2.2):
>   - (a) the `GasRequired` arm of step 2 after failed attempts schedules a `TraceGas` pass at the same
>     temperature, which `Solve` sends to the trace-gas entry instead of `ConvergenceSequence.Run`,
>     and closes with the same `Close`;
>   - (b) a bracket end of kind `TraceGas` selects the final in the one switch of
>     `TemperatureBracket.LeverFinal`.

Coder 3 then filled (b) and added (b′): the text of both is in `## Constraints`.
>   - The trace-gas entry follows the kernel rules, has one `NoInlining` call site, and on `Ok` leaves
>     `IterationState`, scratch and result as a converged `ConvergenceSequence` does. It does not
>     reorder this ladder or change the bracket's numbers, and does not touch `BracketEnds`.

---

<a id="recheck-ok"></a>

## 2026-10-04 — from "## Invariants" — an Ok attempt ends the case untouched

Coder 2 of 0.2.2, the addendum of the no-ice investigation: a cold hp or sp attempt that ends `Ok` below a dead-end floor is rechecked (`## Constraints`, "Dead-end floors"). The wording of the invariant stood:

> - **An `Ok` attempt ends the case untouched.** The plan acts only after a failure: a case whose
>   first attempt or cold fallback ends `Ok` ends with the bits it had before 0.2.2.
