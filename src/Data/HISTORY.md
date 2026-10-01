# HISTORY.md — src/Data

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

The entry below was moved here on 2026-10-01 from the root `HISTORY.md`, with the root
constraint it belongs to.

---

<a id="data-embedded"></a>

## 2026-10-01 — from "## Constraints", "Data" — the NASA files are embedded in the data assembly

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (distribution phase): stood "they are read at run time from that
> directory or from a path given by the caller". A NuGet package and a .NET tool have
> no `data/` directory beside them, so every consumer would have to find NASA files
> before the first call. Embedding the committed files keeps the data-from-files
> invariant, because the bytes are the committed ones with their hash recorded. The
> caller's path stays for other databases. The command line's search for `data/`
> beside the executable and in the current directory goes with it; its `API.md`
> records the change.

---
