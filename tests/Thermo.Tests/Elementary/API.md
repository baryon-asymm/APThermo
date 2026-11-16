# API.md — Thermo.Tests.Elementary

The node exposes nothing outward. Its contract points upward: what the parent may consider proven about
[Thermo.Elementary](../../../src/Thermo/Elementary/API.md).

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| `KernelMath.Exp`, `Log` and `Pow` are correctly rounded on every input of the fixtures (the constructed families and a selection of CORE-MATH's worst cases), through the entries and through the accurate paths alone | `CorrectRoundingTests` | ✅ 2026-10-05 |
| the same on every input of CORE-MATH's full lists, 2 266 122 of them (optional, `APTHERMO_COREMATH_WC`) | `FullWorstCaseListsTests` | ✅ 2026-10-05 |
| the fast paths' relative errors stay at least four times below the bounds the rounding test uses | `FastPathMarginTests` | ✅ 2026-10-05 |
| the special values equal the runtime's; the tables and constants hold their identities; no static state, `NoInlining` on every entry | `SpecialValueTests`, `TableIdentityTests`, `StructureTests` | ✅ 2026-10-05 |

## What the tests rely on

- `fixtures/*.txt`: bit patterns, the oracle's results and exact values (`fixtures/PROVENANCE.txt` names the source, the
  commit, the licence and the selection rule of the worst-case files).
- Python 3.8+ standard library for the default portable full-corpus refusal controls;
  mpmath 1.3.0 additionally for the generators and the optional numerical full check.
- ⚠ 2026-11-14: the previous default-run statement excluded Python entirely. The new
  automated integrity self-check uses Python without importing mpmath. Both full-campaign
  halves verify the pinned raw SHA-256 identities, strict format and usable counts from
  `fixtures/PROVENANCE.txt` before producing inputs or reading numerical outputs.
  `identity` checks those prerequisites alone; it does not establish correct rounding.

⚠ 2026-11-16: the Python process used by the full-list and integrity facts previously
had no deadline covering its exit and inherited streams. It now has a five-minute
run deadline and at most five seconds of cleanup; on timeout, partial streams are
attached to the exception. If the direct parent has exited, its descendants are not
identified by its process handle and may survive; the diagnostic states that limit.
