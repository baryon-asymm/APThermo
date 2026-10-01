# HISTORY.md — Harness

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` (or `ACCEPTANCE.md`) at the place the text
used to stand.

---

<a id="compares-bits"></a>

## 2026-10-01 — from "## Invariants" — "this node compares bits" and JsonFieldComparison

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-30: "this node compares bits" stopped being true when `JsonFieldComparison`
>   moved here: it compares documents field by field, numbers within 1e-9 relative. That
>   figure is not a tolerance derived here; it is the rule the root's Documentation bullet
>   of 2026-09-29 sets for the command-line examples (four orders above the measured
>   platform difference, five below the fixtures' rows), held in one place because two
>   consumers, the docs tests node and the workflow step that runs the packed tool, apply
>   it. The bit comparison of `Bits` and `BitHash` is unchanged.

---

<a id="differences-fields"></a>

## 2026-10-01 — from "## Constraints" — Bits.Differences read properties only

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   - ⚠ It read properties only, while its contract says fields. On a struct of public
>     fields it compared nothing: a tuple `(1e6, 3000)` against `(2e6, NaN)` returned no
>     difference. The tree's descriptor structs are public-field structs, and no caller
>     passes one today.

---

<a id="families-keys"></a>

## 2026-10-01 — from "## Constraints" — FixtureFamilies.Of split into Keys and CasesOf

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-25: `FixtureFamilies.Of` returned `TheoryData<string, int, IReadOnlyList<CeaCase>>`, so
>   fixing the Diagnostics constraint's xUnit1042 in this node meant a `PackageReference` to `xunit`
>   for the return type alone, and the fix then failed xUnit1045: a `CeaCase` collection is not a
>   type xUnit knows how to serialize for Test Explorer's row enumeration, only the fields of
>   `TheoryData<>` itself. Adding `xunit` here was rejected on review: the protocol tests node's
>   `SurfaceTests` tells a library assembly from a test one by whether it references any assembly
>   named `xunit*` (`NodeAssemblies.IsTestAssembly`), not by this node's own `IsTestProject false`,
>   so it would have read as a test assembly and dropped its whole public surface out of
>   `PublicSurface.approved.txt` — weakening the surface check is not this node's decision. `Of` is
>   instead `Keys` (the key and case count of every family, both types xUnit already serializes)
>   plus `CasesOf` (the family's cases, read back inside the test body, the pattern
>   `HostSolver.CaseNames`/`Load` already uses for a single case); each caller (`Equilibrium.Tests`,
>   `Performance.Tests`, `Transport.Tests`, all of which reference xunit already) builds its own
>   `TheoryData<string, int>` from `Keys` in a static member of its test class. This node names no
>   test framework, as the sentence above states.

---

<a id="bits-reference-machine"></a>

## 2026-10-01 — from "## Invariants" — bits compared on the reference machine only

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-19: the root BOOT.md's platform constraint (⚠ 2026-09-18, the declared
>   deviation from "every test runs on both platforms") holds that the bits are a
>   record of the reference machine, not of the platform alone, and are compared
>   exactly only there: in local runs and on the release's self-hosted jobs
>   (`.github/workflows/release.yml`'s `cuda-windows` and `cuda-linux`, filter
>   `Category=Cuda|Category=BitSnapshot`), never on the hosted CI runners (`ci.yml`;
>   `release.yml`'s `matrix` job; filter `Category!=LongRunning&Category!=BitSnapshot`).
>   Each Bits-level consumer's own fact carries `[Trait("Category", "BitSnapshot")]`
>   (`Cli.Tests`, `Equilibrium.Tests`, `Performance.Tests`, `Problems.Tests`,
>   `Thermo.Tests`, `Transport.Tests`); this node adds no trait and no CI knowledge of
>   its own — `ApprovedSnapshot` still only picks the file, never who runs the test
>   that reads it.

---

<a id="crlf-linux"></a>

## 2026-10-01 — from "## Invariants" — the actual file's line endings on Linux

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-17: "the Windows platform this tree targets" was true when this bullet was
>   written (2026-09-14), narrower than the root's platform constraint since the
>   2026-09-15 distribution-phase decision added Linux x64. On Linux `Environment.NewLine`
>   is `"\n"`, so an actual file this node writes there is LF like the repository, not
>   CRLF; the normalize-before-approving step only bites on Windows. Found while adding
>   the per-platform bit snapshots below.

---

<a id="stale-keys"></a>

## 2026-10-01 — from "## Invariants" — StaleKeys returns bare keys

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-15: this bullet read "a key missing from the approved file, a differing
>   line and an approved key that no run produced are each a problem naming the key and
>   how to approve; on a problem the actual lines are written". True of `Problem`, not of
>   `StaleKeys`: that method only returns bare keys (`ApprovedSnapshot.cs`), and wording
>   them into a problem and writing an actual file is left to the caller, which is why two
>   consumers word it themselves and, until 2026-09-15, four never called it at all.
>   Found by the repair review of 2026-09-15 reading the code against the claim.

---
