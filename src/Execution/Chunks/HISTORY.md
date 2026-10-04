# HISTORY.md — Execution.Chunks

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="input-output-2026-10-04"></a>

## 2026-10-04 — from "## Purpose" — five declaration methods

`ChunkBuffers` gained a sixth declaration method, `InputOutput`, for the moles buffer of a seeded equilibrium batch, which the kernel reads and writes. The paragraph as it stood:

> The rest of `src/Execution` reaches this through `ChunkPlan.For`/`.Chunks()`,
> `ChunkBuffers`'s five declaration methods, `Allocate`, `UploadChunk`, `DownloadChunk`,
> `BytesPerCase` and `MaxElementsPerCase` (2026-09-26, the audit's F4), and

---

<a id="doubles-per-case-2026-10-03"></a>

## 2026-10-03 — from "## Constraints" — the worked figure of the element cap

The figure of the 32-bit offset cap's worked example was stale since 2026-09-26: `ScratchLayout.DoublesPerCase` at `TableLimits` (2048 species, 20 elements) was already 16 264 when the wording of 13 248 stood, and 0.2.2 added two doubles per species for the temperature bracket's ends (`Equilibrium` `BOOT.md`, "Structure"), making it 9 · 2048 + 20 + 3 · 20 + 42² + 2 · 42 = 20 360. The fact `ChunksStayWithinInt32OffsetsAtTableLimits` types its own planner input (13 248) and is not a measurement of the layout, so it stays. The original wording follows.

>   - ⚠ Only bytes bounded a chunk. With `ScratchBytes` above 16 GiB, legal on a large
>     GPU or on the CPU accelerator, a table at `TableLimits` (13 248 doubles per case)
>     wrapped the offset at case 162 100.
